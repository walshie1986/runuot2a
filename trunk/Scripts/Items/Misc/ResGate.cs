using System;
using Server.Gumps;

namespace Server.Items
{
	public class ResGate : Item
	{
		private bool m_refresh;
		
		public override string DefaultName
		{
			get { return "a resurrection gate"; }
		}
		
		[CommandProperty( AccessLevel.GameMaster )]
		public bool Refresh
		{
			get{ return m_refresh; }
			set{ m_refresh = value;}
		}

		[Constructable]
		public ResGate() : base( 0xF6C )
		{
			Movable = false;
			Hue = 0x2D1;
			Light = LightType.Circle300;
		}

		public ResGate( Serial serial ) : base( serial )
		{
		}

		public override bool OnMoveOver( Mobile m )
		{
			if(m_refresh) {
				m.PlaySound( 0x214 );
				m.FixedEffect( 0x376A, 10, 16 );

				m.Resurrect();
				
				m.Hits = m.HitsMax;
				m.Stam = m.Dex;
				m.Mana = m.Int;
			} else {
				if ( !m.Alive && m.Map != null && m.Map.CanFit( m.Location, 16, false, false ) )
				{
					m.PlaySound( 0x214 );
					m.FixedEffect( 0x376A, 10, 16 );
	
					m.CloseGump( typeof( ResurrectGump ) );
					m.SendGump( new ResurrectGump( m ) );
				}
				else
				{
					m.SendLocalizedMessage( 502391 ); // Thou can not be resurrected there!
				}
			}

			return false;
		}

		public override void Serialize( GenericWriter writer )
		{
			base.Serialize( writer );

			writer.Write( (int) 1 ); // version
			writer.Write( m_refresh );
		}

		public override void Deserialize( GenericReader reader )
		{
			base.Deserialize( reader );

			int version = reader.ReadInt();
			if(version > 0) {
				m_refresh = reader.ReadBool();
			}
		}
	}
}
