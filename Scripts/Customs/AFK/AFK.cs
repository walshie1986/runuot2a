/*
 * Created by SharpDevelop.
 * User: Ben
 * Date: 30/07/2009
 * Time: 5:24 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using System.Xml;
using Server.Mobiles;
using Server.Accounting;
using Server.Network;
using Server.Commands;
using Server.Gumps;

namespace Server.Scripts.Customs.AFK
{
	public enum CheckState
	{
		Picked,
		Notified,
		Challenged,
		Reconnected,
		BotCheck,
		BotChallenge
	}
	public class CheckEntry
	{
		public PlayerMobile m;
		public DateTime expire;
		public int answer;
		public int param;
		public CheckState state;
		
		public CheckEntry(PlayerMobile m)
		{
			this.m = m;
			expire = DateTime.Now;
			state = CheckState.Picked;
		}
	}
	/// <summary>
	/// Description of AFK.
	/// </summary>
	public class AFK
	{
		public static int ApproxPlayers = 10;
		public static int MinChecks = 10; //1% of people
		public static int MaxChecks = 50; //5% of people
		public static AFK Inst;
		
		private Dictionary<PlayerMobile, CheckEntry> checks = new Dictionary<PlayerMobile, CheckEntry>();
		
		private List<CheckEntry> GetChecks {
			get { return new List<CheckEntry>(checks.Values); }
		}
		private DateTime nexCheck = DateTime.Now + TimeSpan.FromMinutes(5);
		//Needs to select some logged on players depending on criteria
		
		public static void Initialize()
		{
			if(Inst == null)
				Inst = new AFK();
			EventSink.Login += new LoginEventHandler( EventSink_Login );
		}
		
		public AFK()
		{
			Timer.DelayCall(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(30), new TimerCallback( Tick ));
			
			CommandSystem.Register( "afk", AccessLevel.Player, new CommandEventHandler( ChallengeNow ) );
			CommandSystem.Register( "afkTest", AccessLevel.Administrator, new CommandEventHandler( ChallengeNow2 ) );
		}
		
		public void ChallengeNow2( CommandEventArgs e )
		{
			PlayerMobile pm = e.Mobile as PlayerMobile;
			if(pm == null)
				return;
			checks.Remove(pm);
			CheckEntry en = new CheckEntry(pm);
			checks.Add(pm, en);
		}
		
		public void ChallengeNow( CommandEventArgs e )
		{
			if(e.Mobile is PlayerMobile)
			{
				PlayerMobile pm = (PlayerMobile)e.Mobile;
				if(checks.ContainsKey(pm))
				{
					CheckEntry entry = checks[pm];
					if(entry.state == CheckState.Notified)
					{
						Challenge(entry);
					} else
					{
						pm.SendMessage("This is the wrong time to send this command.");
					}
				} else
				{
					pm.SendMessage("You are not selected for AFK testing");
				}
			}
		}
		
		private static void EventSink_Login( LoginEventArgs args )
		{
			PlayerMobile pm = args.Mobile as PlayerMobile;
			Account a = pm.Account as Account;
			
			if(pm != null && a != null)
			{				
				//Check for disconnected flag
				if(a.GetTag("AFKTag-Selected") != null)
				{
					Reconnected(pm);
				} else
				{
					String failedTime = a.GetTag("AFKTag-FailedTime");
					if(failedTime != null)
					{
						pm.SendMessage(0x21, "Please confirm you are not a bot.");
						CheckEntry e = new CheckEntry(pm);
						e.state = CheckState.BotCheck;
						if(Inst.checks.ContainsKey(pm))
							Inst.checks[pm] = e;
						else
							Inst.checks.Add(pm, e);
						/*if(Utility.GetDateTime( failedTime, DateTime.MinValue ) > DateTime.Now + TimeSpan.FromMinutes(30))
						{
							pm.SendMessage(0x21, "You have reconnected in the AFK grace period. Please authenticate now.");
							Reconnected(pm);
							//Grace time
						} else
						{
							pm.SendMessage(0x21, "Your account has been flagged by the AFK detection system.");
							IncrementFlag(pm);
							a.RemoveTag("AFKTag-FailedTime");
							//Too bad
						}*/
					}
				}
			}
		}
		
		private static void Reconnected(PlayerMobile pm)
		{
			CheckEntry e = new CheckEntry(pm);
			e.state = CheckState.Reconnected;
			if(Inst.checks.ContainsKey(pm))
				Inst.checks[pm] = e;
			else
				Inst.checks.Add(pm, e);
		}
		
		private static void IncrementFlag(PlayerMobile pm)
		{
			Account a = pm.Account as Account;
			if (a == null)
				return;
			
			String flag = a.GetTag("AFKTag-Level");
			int level = flag == null ? 0 : int.Parse(flag);
			a.SetTag("AFKTag-LastInc", XmlConvert.ToString( DateTime.Now, XmlDateTimeSerializationMode.Local ));
			
			switch(level)
			{
				case 3:
					//Big trouble
					pm.SendMessage(0x21, "You have been banned for 1 hour for AFK macroing");
					a.SetBanTags(null, DateTime.Now, TimeSpan.FromHours(1));
					Timer.DelayCall(TimeSpan.FromSeconds(15), new TimerStateCallback<PlayerMobile>(Kick), pm);
					break;
				case 2:
					//Minor trouble
					pm.SendMessage(0x21, "You have been banned for 5 minutes for AFK macroing");
					a.SetBanTags(null, DateTime.Now, TimeSpan.FromMinutes(5));
					Timer.DelayCall(TimeSpan.FromSeconds(15), new TimerStateCallback<PlayerMobile>(Kick), pm);
					a.SetTag("AFKTag-Level", "3"); //disable for now. Max 5 minute ban.
					break;
				case 1:
					//Permanent note
					pm.SendMessage(0x21, "Your account has been flagged for AFK macroing. Next time you will be temporarily banned.");
					a.SetTag("AFKTag-Level", "2");
					break;
				default:
					//Warning
					pm.SendMessage(0x21, "You have been caught AFK macroing. If you believe this is an error, please page a GM.");
					a.SetTag("AFKTag-Level", "1");
					break;
			}
		}
		
		internal class SelectionEntry : IComparable<SelectionEntry>
		{
			public SelectionEntry(PlayerMobile m, float score)
			{
				this.m = m;
				this.score = score;
			}
			public PlayerMobile m;
			public float score;
			
			public int CompareTo(SelectionEntry other)
			{
				return score.CompareTo(other.score);
			}
		}
		
		public void Run()
		{			
			nexCheck = DateTime.Now + TimeSpan.FromMinutes( (Utility.RandomDouble() * 5) + 5); //5 - 10 Minutes;
			List<SelectionEntry> selection = new List<SelectionEntry>(ApproxPlayers);
			
			foreach(NetState ns in NetState.Instances)
			{
				Account a = ns.Account as Account;
				PlayerMobile m = ns.Mobile as PlayerMobile;
				
				if(a == null || m == null || a.AccessLevel > AccessLevel.Player || a.GetTag("AFKTag-Excempt") != null || a.GetTag("AFKTag-Selected") != null || a.GetTag("AFKTag-FailedTime") != null )
					continue;

				SelectionEntry n = new SelectionEntry(m, AFKScore(m));
				selection.Add(n);
				//Add all logged in characters.
			}
			
			selection.Sort();
			
			int count = Math.Max((Utility.RandomMinMax(MinChecks, MaxChecks) * selection.Count) / 1000, 5);
			
			while(count > 0 && selection.Count > 0)
			{
				SelectionEntry e = selection[selection.Count - 1]; //Get highest score
				if(e.score < 0)
					break;
				PlayerMobile pm = e.m;
				selection.RemoveAt(selection.Count - 1);
				if(pm == null || pm.Deleted || pm.Map == Map.Internal || pm.NetState == null)
					continue;
				count--;
				
				Account a = pm.Account as Account;
				
				checks.Add(pm, new CheckEntry(pm));
				if (a != null)
					a.AddTag("AFKTag-Selected", "true");		
			}
		}
		
		//High score for ones that need to be checked.
		private float AFKScore( PlayerMobile m )
		{
			Account a = m.Account as Account;
			if(a == null)
				return (float)-1.0;
			
			DateTime lastChecked, lastInc;
			int flagLevel = 0;
			
			String lastTime = a.GetTag("AFKTag-LastChecked");
			if(lastTime != null)
				lastChecked = Utility.GetDateTime( lastTime, DateTime.MinValue );
			else
				lastChecked = DateTime.MinValue;
			
			String sLastInc = a.GetTag("AFKTag-LastInc");
			if(sLastInc != null)
				lastInc = Utility.GetDateTime( sLastInc, DateTime.MinValue );
			else
				lastInc = DateTime.MinValue;
			
			String sFlagLevel = a.GetTag("AFKTag-Level");
			if(sFlagLevel != null)
			{
				flagLevel = int.Parse(sFlagLevel);
				if(sLastInc != null && DateTime.Now > lastInc.AddDays(7))
				{
					flagLevel--;
					if(flagLevel == 0)
						a.RemoveTag("AFKTag-Level");
					else
					{
						a.SetTag("AFKTag-Level", flagLevel.ToString());
						a.SetTag("AFKTag-LastInc", XmlConvert.ToString( DateTime.Now, XmlDateTimeSerializationMode.Local ));
					}
				}
			}
			
			return (float)((Math.Max(1, flagLevel)*DateTime.Now.Subtract(lastChecked).TotalHours)-4.0); //4 hours grace if recently checked.
		}
		
		public static void Kick(PlayerMobile pm)
		{
			if( pm != null && pm.NetState != null )
				pm.NetState.Dispose();
		}
		
		public void Tick()
		{
			if(nexCheck < DateTime.Now)
				Run();
						
			foreach(CheckEntry e in GetChecks)
			{
				if(e.m == null || e.m.Deleted || e.m.NetState == null || e.m.Map == Map.Internal)
				{
					checks.Remove(e.m);
					continue;
				}
				
				switch(e.state)
				{
					case CheckState.Picked:
						//Notify
						e.expire = DateTime.Now + TimeSpan.FromMinutes(30);
						e.state = CheckState.Notified;
						e.m.SendMessage(0x21, "You have been randomly selected for human operator testing.");
						goto case CheckState.Notified;
					case CheckState.Notified:
						if(e.expire > DateTime.Now)
						{
							//e.m.SendSound( SomeLoudSound );
							e.m.SendMessage(0x21, String.Format("You will be tested in {0:F1} minutes. Type [afk to take the test now.", e.expire.Subtract(DateTime.Now).TotalMinutes));
							continue;
						}
						goto case CheckState.Reconnected;
					case CheckState.Reconnected:
						Challenge(e);
						break;
					case CheckState.BotChallenge:
						goto case CheckState.Challenged;
					case CheckState.Challenged:
						if(e.expire < DateTime.Now)
						{
							e.param = 10;
							BadAnswer(e);
						}
						break;
					case CheckState.BotCheck:
						Challenge(e);
						e.state = CheckState.BotChallenge;
						break;
					default:
						break;
				}
			}
		}
		
		public void Challenge(CheckEntry e)
		{
			e.expire = DateTime.Now + TimeSpan.FromMinutes(5);
			e.param = 0;
			e.state = CheckState.Challenged;
			//Challenge
			e.m.SendGump(new AFKGump(e));
		}
			
		public void BadAnswer(CheckEntry e)
		{
			if(e == null)
				return;
			
			e.param++;
			if(e.param > 1)
			{
				e.m.SendMessage("Too many wrong selections. Kicking.");
				checks.Remove(e.m);
				Kick(e.m);
				
				Account a = e.m.Account as Account;
				if( a == null)
					return;
				
				if(e.state == CheckState.BotChallenge)
				{
					//Increment flag
					IncrementFlag(e.m);
				} else
				{
					a.RemoveTag("AFKTag-Selected");
					if(a.GetTag("AFKTag-FailedTime") == null)
						a.AddTag("AFKTag-FailedTime", XmlConvert.ToString( DateTime.Now, XmlDateTimeSerializationMode.Local ));
				}
			} else
			{
				e.m.SendGump(new AFKGump(e));
				e.m.SendMessage("Wrong selection. Please try again.");
			}
		}
		
		public void GoodAnswer(CheckEntry e)
		{
			if(e == null)
				return;
			
			checks.Remove(e.m);
			Account a = e.m.Account as Account;
			
			if(a == null)
				return;
			
			a.RemoveTag("AFKTag-Selected");
			a.SetTag("AFKTag-LastChecked", XmlConvert.ToString( DateTime.Now, XmlDateTimeSerializationMode.Local ));
			a.RemoveTag("AFKTag-FailedTime");
			e.m.SendMessage("Confirmed. Thank you.");
		}
	}
	
	public class AFKGump : Gump
	{
		private static int m_initX = 45;
		private static int m_initY = 125;
		private static int m_incrX = 80;
		private static int m_incrY = 80;
		
		private CheckEntry m_entry;
		
		public AFKGump(CheckEntry e) : base(100, 20)
		{
			m_entry = e;
			
			Closable = false;
			
			
			int answer = Utility.Random(12);
			int spellAnswer = Utility.Random(64);
			
			if(e != null)
				e.answer = answer;
			
			AddPage( 0 );

			AddBackground( 0, 0, 400, 400, 2600 );

			AddHtml( 10, 20, 390, 20, Color( Center( "Click this spell from the 12 below." ), 0xFFFFFF ), false, false );
			AddImage(165, 45, 7000+spellAnswer);

			for(int i = 0; i < 12; i++)
			{
				int spell;
				if(i == answer)
				{
					spell = spellAnswer;
				} else
				{
					do
					{
						spell = Utility.Random(64);
					} while(spell == spellAnswer);
				}
				AddButton(m_initX + (i%4)*m_incrX, m_initY + (i/4)*m_incrY, 7000 + spell, 7000 + spell, i, GumpButtonType.Reply, 0);
			}
		}
		
		public override void OnResponse( NetState state, RelayInfo info )
		{
			Mobile from = state.Mobile;
			
			//from.SendMessage("You have clicked button: " + info.ButtonID.ToString() + ". Answer is: " + m_entry.answer.ToString());
			if(info.ButtonID == m_entry.answer)
			{
				AFK.Inst.GoodAnswer(m_entry);
			} else
			{
				AFK.Inst.BadAnswer(m_entry);
			}
		}
		
		public string Center( string text )
		{
			return String.Format( "<CENTER>{0}</CENTER>", text );
		}

		public string Color( string text, int color )
		{
			return String.Format( "<BASEFONT COLOR=#{0:X6}>{1}</BASEFONT>", color, text );
		}
	}
}
