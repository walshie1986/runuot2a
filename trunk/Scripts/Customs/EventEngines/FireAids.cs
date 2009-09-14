using System;
using System.Collections.Generic;
using Server.Targeting;

namespace Server.Items
{
	/// <summary>
	/// Description of FireAids.
	/// </summary>
	public class FireAids : Item
	{
		public FireAids() : this( 1 )
		{
		}

		public FireAids( int amount ) : base( 0xE21 )
		{
			Stackable = true;
			Amount = amount;
		}
		
		public override void OnDoubleClick( Mobile from )
		{
			if ( from.InRange( GetWorldLocation(), 1 ) )
			{
				from.RevealingAction();

				from.SendMessage( "Who would you like to thaw?" ); // Who will you use the bandages on?

				from.Target = new InternalTarget( this );
			}
			else
			{
				from.SendLocalizedMessage( 500295 ); // You are too far away to do that.
			}
		}
		
		private class InternalTarget : Target
		{
			private FireAids m_Bandage;

			public InternalTarget( FireAids bandage ) : base( 1, false, TargetFlags.Beneficial )
			{
				m_Bandage = bandage;
			}

			protected override void OnTarget( Mobile from, object targeted )
			{
				if ( m_Bandage.Deleted )
					return;

				if ( targeted is Mobile )
				{
					if ( from.InRange( m_Bandage.GetWorldLocation(), Bandage.Range ) )
					{
						if(((Mobile)targeted).Frozen)
						{
							if ( FireAidsContext.BeginHeal( from, (Mobile)targeted ) != null )
							{
								m_Bandage.Consume();
							}
						} else
						{
							from.SendMessage("That is not frozen");
						}
					}
					else
					{
						from.SendLocalizedMessage( 500295 ); // You are too far away to do that.
					}
				}
				else
				{
					from.SendLocalizedMessage( 500970 ); // Bandages can not be used on that.
				}
			}
		}
	}

	public class FireAidsContext
	{
		private Mobile m_Healer;
		private Mobile m_Patient;
		private Timer m_Timer;

		public Mobile Healer{ get{ return m_Healer; } }
		public Mobile Patient{ get{ return m_Patient; } }
		public Timer Timer{ get{ return m_Timer; } }

		public FireAidsContext( Mobile healer, Mobile patient, TimeSpan delay )
		{
			m_Healer = healer;
			m_Patient = patient;

			m_Timer = new InternalTimer( this, 5 );
			m_Timer.Start();
		}

		public void StopHeal()
		{
			m_Table.Remove( m_Healer );

			if ( m_Timer != null )
				m_Timer.Stop();

			m_Timer = null;
		}

		private static Dictionary<Mobile, FireAidsContext> m_Table = new Dictionary<Mobile, FireAidsContext>();

		public static FireAidsContext GetContext( Mobile healer )
		{
			FireAidsContext bc = null;
			m_Table.TryGetValue( healer, out bc );
			return bc;
		}

		public void EndHeal()
		{
			StopHeal();

			int healerNumber = -1, patientNumber = -1;
			bool playSound = true;
			bool checkSkills = false;

			if ( !m_Healer.Alive || m_Healer.Frozen )
			{
				m_Healer.SendMessage("You were frozen before finishing");
				return;
			}
			else if ( !m_Healer.InRange( m_Patient, 1 ) )
			{
				m_Healer.SendMessage("You did not stay close enough");
				return;
			}
			else
			{
				m_Patient.Frozen = false;
				m_Patient.SolidHueOverride = -1;
				m_Patient.Blessed = false;
			}
		}

		private class InternalTimer : Timer
		{
			private FireAidsContext m_Context;
			private int ticks;

			public InternalTimer( FireAidsContext context, int ticks ) : base( TimeSpan.Zero, TimeSpan.FromSeconds(1) )
			{
				m_Context = context;
				Priority = TimerPriority.FiftyMS;
				this.ticks = ticks;
			}

			protected override void OnTick()
			{
				if(ticks > 0 && !m_Context.Healer.Frozen)
				{
					m_Context.Patient.PublicOverheadMessage(Server.Network.MessageType.Regular, 0x57, true, String.Format("{0}", ticks));
					ticks--;
				} else
				{
					m_Context.EndHeal();
				}
			}
		}

		public static FireAidsContext BeginHeal( Mobile healer, Mobile patient )
		{
			if(patient == null || healer == null || healer == patient || healer.Frozen || !healer.Alive || !patient.Alive || !patient.Frozen)
				return null;

			FireAidsContext context = GetContext( healer );

			if ( context != null )
				context.StopHeal();

			context = new FireAidsContext( healer, patient, TimeSpan.FromSeconds( 5.0 ) );

			m_Table[healer] = context;

			patient.SendLocalizedMessage( 1008078, false, healer.Name ); //  : Attempting to heal you.

			healer.SendMessage( "You begin thawing them" );
			return context;
		}
	}
}
