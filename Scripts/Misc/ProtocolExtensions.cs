using System;
using System.Collections.Generic;
using Server;
using Server.Network;
using Server.Mobiles;
using Server.Engines.PartySystem;
using Server.Commands;

namespace Server.Misc
{
	public class ProtocolExtensions
	{
		private static PacketHandler[] m_Handlers = new PacketHandler[0x100];
		private static Dictionary<string, NegotiateTimer> m_NegTimers = new Dictionary<string, NegotiateTimer>();

		public static void Initialize()
		{
			PacketHandlers.Register( 0xF0, 0, false, new OnPacketReceive( DecodeBundledPacket ) );

			Register( 0x00, true, new OnPacketReceive( QueryPartyLocations ) );
			Register( 0xFF, true, new OnPacketReceive( NegotiateResponse ) );
			
			EventSink.Login += new LoginEventHandler( EventSink_Login );
			EventSink.Logout += new LogoutEventHandler( EventSink_Logout );
		}
		
		private static void EventSink_Logout( LogoutEventArgs args )
		{
			Mobile mobile = args.Mobile;
			if(m_NegTimers.ContainsKey(mobile.Account.Username.ToString()))
			{
				m_NegTimers[mobile.Account.Username.ToString()].Stop();
				m_NegTimers.Remove(mobile.Account.Username.ToString());
			}
		}
		private static void EventSink_Login( LoginEventArgs args )
		{
			Mobile mobile = args.Mobile;
			if(m_NegTimers.ContainsKey(mobile.Account.Username.ToString()))
			{
				m_NegTimers[mobile.Account.Username.ToString()].Stop();
				m_NegTimers.Remove(mobile.Account.Username.ToString());
			}
			if(mobile.AccessLevel == AccessLevel.Player) 
			{
				args.Mobile.NetState.Send(new RazorFeatures());
				(new LightTimer(args.Mobile)).Start();
				NegotiateTimer negtimer = new NegotiateTimer(mobile, m_NegTimers);
				negtimer.Start();
				m_NegTimers.Add(mobile.Account.Username.ToString(),negtimer);
			}
		}
		
		public static void NegotiateResponse( NetState state, PacketReader pvSrc )
		{
			String userName = state.Account.Username.ToString();
			if(m_NegTimers.ContainsKey(userName))
			{
				state.Mobile.SendAsciiMessage(0x35, "Client verfication complete");
				m_NegTimers[userName].Stop();
				m_NegTimers.Remove(userName);
			}
		}

		public static void QueryPartyLocations( NetState state, PacketReader pvSrc )
		{
			Mobile from = state.Mobile;
			Party party = Party.Get( from );

			if ( party != null )
			{
				AckPartyLocations ack = new AckPartyLocations( from, party );

				if ( ack.UnderlyingStream.Length > 8 )
					state.Send( ack );
			}
		}

		public static void Register( int packetID, bool ingame, OnPacketReceive onReceive )
		{
			m_Handlers[packetID] = new PacketHandler( packetID, 0, ingame, onReceive );
		}

		public static PacketHandler GetHandler( int packetID )
		{
			if ( packetID >= 0 && packetID < m_Handlers.Length )
				return m_Handlers[packetID];

			return null;
		}

		public static void DecodeBundledPacket( NetState state, PacketReader pvSrc )
		{
			int packetID = pvSrc.ReadByte();

			PacketHandler ph = GetHandler( packetID );

			if ( ph != null )
			{
				if ( ph.Ingame && state.Mobile == null )
				{
					Console.WriteLine( "Client: {0}: Sent ingame packet (0xF0x{1:X2}) before having been attached to a mobile", state, packetID );
					state.Dispose();
				}
				else if ( ph.Ingame && state.Mobile.Deleted )
				{
					state.Dispose();
				}
				else
				{
					ph.OnReceive( state, pvSrc );
				}
			}
		}
	}

	public abstract class ProtocolExtension : Packet
	{
		public ProtocolExtension( int packetID, int capacity ) : base( 0xF0 )
		{
			EnsureCapacity( 4 + capacity );

			m_Stream.Write( (byte) packetID );
		}
	}

	public class AckPartyLocations : ProtocolExtension
	{
		public AckPartyLocations( Mobile from, Party party ) : base( 0x01, ((party.Members.Count - 1) * 9) + 4 )
		{
			for ( int i = 0; i < party.Members.Count; ++i )
			{
				PartyMemberInfo pmi = (PartyMemberInfo)party.Members[i];

				if ( pmi == null || pmi.Mobile == from )
					continue;

				Mobile mob = pmi.Mobile;

				if ( Utility.InUpdateRange( from, mob ) && from.CanSee( mob ) )
					continue;

				m_Stream.Write( (int) mob.Serial );
				m_Stream.Write( (short) mob.X );
				m_Stream.Write( (short) mob.Y );
				m_Stream.Write( (byte) (mob.Map == null ? 0 : mob.Map.MapID) );
			}

			m_Stream.Write( (int) 0 );
		}
	}
	
	public class RazorFeatures : ProtocolExtension
	{
		[Flags]
		private enum RazorFlags {
			None					= 0x00000000,
			WeatherFilter			= 0x00000001,
			LightFilter				= 0x00000002,
			SmartLT					= 0x00000004,
			RangeCheckLT			= 0x00000008,
			AutoOpenDoors			= 0x00000010,
			UnequipBeforeCast		= 0x00000020,
			AutoPotionEquip			= 0x00000040,
			BlockHealPoisoned		= 0x00000080,
			LoopingMacros			= 0x00000100,
			UseOnceAgent			= 0x00000200,
			RestockAgent			= 0x00000400,
			SellAgent				= 0x00000800,
			BuyAgent				= 0x00001000,
			PotionHotkeys			= 0x00002000,
			RandomTargets			= 0x00004000,
			ClosestTargets			= 0x00008000,
			OverheadHealth			= 0x00010000,
		}
		
		public RazorFeatures() : base( 0xfe, 12 )
		{
			RazorFlags flags = RazorFlags.WeatherFilter | RazorFlags.LightFilter | RazorFlags.BlockHealPoisoned | RazorFlags.RangeCheckLT;
			m_Stream.Write( (int) 0);
			m_Stream.Write( (int) flags );
		}
	}
	
	internal class LightTimer : Timer
	{
		private Mobile m_Mobile;
		
		public LightTimer(Mobile m) : base( TimeSpan.FromSeconds(10) )
		{
			m_Mobile = m;
		}
		
		protected override void OnTick()
		{
			m_Mobile.CheckLightLevels(true);
			Stop();
		}
	}
	
	internal class NegotiateTimer : Timer
	{
		private Mobile m_Mobile;
		private int count;
		private Dictionary<string, NegotiateTimer> list;
		
		public NegotiateTimer(Mobile m, Dictionary<string, NegotiateTimer> list) : base( TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) )
		{
			m_Mobile = m;
			count = 3;
			this.list = list;
		}
		
		protected override void OnTick()
		{
			if(count-- == 0)
			{
				//Remove timer and disconnect;
				list.Remove(m_Mobile.Account.Username.ToString());
				m_Mobile.NetState.Dispose();
				Stop();
			} else
			{
				//Warn
				m_Mobile.SendAsciiMessage(0x21, "You are either not using Razor or have not enabled 'Negotiate features with server'. Please rectify this and reconnect");
			}
		}
	}
}