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

namespace Server.Scripts.Customs.AFK
{
	public enum CheckState
	{
		Picked,
		Notified,
		Challenged,
		Reconnected
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
			Timer.DelayCall(TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(30), new TimerCallback( Run ));
			
			CommandSystem.Register( "afk", AccessLevel.Player, new CommandEventHandler( ChallengeNow ) );
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
						entry.expire = DateTime.Now;
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
			Mobile m = args.Mobile;
			Account a = (Account)m.Account;
			
			if(m is PlayerMobile)
			{
				PlayerMobile pm = (PlayerMobile)m;
				
				//Check for disconnected flag
				if(a.GetTag("AFKTag-Selected") != null)
				{
					Reconnected(pm);
				} else
				{
					String failedTime = a.GetTag("AFKTag-FailedTime");
					if(failedTime != null)
					{
						if(DateTime.Parse(failedTime) > DateTime.Now + TimeSpan.FromMinutes(30))
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
						}
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
			Account a = (Account)pm.Account;
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
					//a.SetTag("AFKTag-Level", "3"); //disable for now. Max 5 minute ban.
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
				Account a = (Account)ns.Account;
				
				if(a.AccessLevel > AccessLevel.Player || a.GetTag("AFKTag-Excempt") != null || a.GetTag("AFKTag-Selected") != null || a.GetTag("AFKTag-FailedTime") != null )
					continue;
				
				if(ns.Mobile is PlayerMobile)
				{
					PlayerMobile m = (PlayerMobile)ns.Mobile;
					SelectionEntry n = new SelectionEntry(m, AFKScore(m));
					selection.Add(n);
					//Add all logged in characters.
				}
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
				
				Account a = (Account)pm.Account;
				
				checks.Add(pm, new CheckEntry(pm));
				a.AddTag("AFKTag-Selected", "true");		
			}
		}
		
		//High score for ones that need to be checked.
		private float AFKScore( PlayerMobile m )
		{
			Account a = (Account)m.Account;
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
				if(sLastInc != null && DateTime.Now > lastInc + TimeSpan.FromDays(7))
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
			
			return (float)((Math.Max(1, flagLevel)*DateTime.Now.Subtract(lastChecked).TotalHours)-2.0); //2 hours grace if recently checked.
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
						break;
					case CheckState.Notified:
						if(e.expire > DateTime.Now)
						{
							//e.m.SendSound( SomeLoudSound );
							e.m.SendMessage(0x21, String.Format("You will be tested in {0:F1} minutes. Type [afk to take the test now.", e.expire.Subtract(DateTime.Now).TotalMinutes;
							continue;
						}
						goto case CheckState.Reconnected;
					case CheckState.Reconnected:
						e.expire = DateTime.Now + TimeSpan.FromMinutes(5);
						e.param = 0;
						e.state = CheckState.Challenged;
						//Challenge
						e.m.SendGump(new AFKGump(e));
						break;
					case CheckState.Challenged:
						if(e.expire < DateTime.Now)
						{
							e.param = 10;
							BadAnswer(e);
						}
						break;
					default:
						break;
				}
			}
		}
			
		public void BadAnswer(CheckEntry e)
		{
			e.param++;
			if(e.param > 1)
			{
				Account a = (Account)e.m.Account;
				a.RemoveTag("AFKTag-Selected");
				if(a.GetTag("AFKTag-FailedTime") == null)
					a.AddTag("AFKTag-FailedTime", DateTime.Now.ToString());
				Kick(e.m);
				checks.Remove(e.m);
			} else
			{
				e.m.SendGump(new AFKGump(e));
			}
		}
		
		public void GoodAnswer(CheckEntry e)
		{
			checks.Remove(e.m);
			Account a = (Account)e.m.Account;
			
			a.SetTag("AFKTag-LastChecked", XmlConvert.ToString( DateTime.Now, XmlDateTimeSerializationMode.Local ));
		}
	}
	
	public class AFKGump : Server.Gumps.Gump
	{
		private CheckEntry m_entry;
		
		public AFKGump(CheckEntry e) : base(100, 0)
		{
			m_entry = e;
			
			AddPage( 0 );

			AddBackground( 0, 0, 400, 350, 2600 );

			AddHtmlLocalized( 0, 20, 400, 35, 1011022, false, false ); // <center>Resurrection</center>

			AddHtmlLocalized( 50, 55, 300, 140, 1011023 + (int)msg, true, true ); /* It is possible for you to be resurrected here by this healer. Do you wish to try?<br>
																				   * CONTINUE - You chose to try to come back to life now.<br>
																				   * CANCEL - You prefer to remain a ghost for now.
																				   */

			AddButton( 200, 227, 4005, 4007, 0, GumpButtonType.Reply, 0 );
			AddHtmlLocalized( 235, 230, 110, 35, 1011012, false, false ); // CANCEL

			AddButton( 65, 227, 4005, 4007, 1, GumpButtonType.Reply, 0 );
			AddHtmlLocalized( 100, 230, 110, 35, 1011011, false, false ); // CONTINUE
		}
	}
}
