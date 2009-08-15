using System;
using Server;
using Server.Spells.Spellweaving;

namespace Server.Items
{
	public abstract class BaseMeleeWeapon : BaseWeapon
	{
		public BaseMeleeWeapon( int itemID ) : base( itemID )
		{
		}

		public BaseMeleeWeapon( Serial serial ) : base( serial )
		{
		}
		
		private static double ReactPercent = 0.5; //How much damage gets through

		public override int AbsorbDamage( Mobile attacker, Mobile defender, int damage )
		{
			damage = base.AbsorbDamage( attacker, defender, damage );

			AttuneWeaponSpell.TryAbsorb( defender, ref damage );

			if ( Core.AOS )
				return damage;
			
			int absorb = defender.MeleeDamageAbsorb;

			if ( absorb > 0 )
			{
				int absorbed = Math.Min(damage, absorb);
				
				defender.MeleeDamageAbsorb -= absorbed;
				if(defender.MeleeDamageAbsorb <= 0)
				{
					defender.MeleeDamageAbsorb = 0;
					defender.SendLocalizedMessage( 1005556 ); // Your reactive armor spell has been nullified.
				}
				
				damage -= (int)(absorbed * ReactPercent);
				attacker.Damage( (int)(absorbed * (1 - ReactPercent)), defender);
				
				attacker.PlaySound( 0x1F1 );
				attacker.FixedEffect( 0x374A, 10, 16 );
			}

			return damage;
		}

		public override void Serialize( GenericWriter writer )
		{
			base.Serialize( writer );
		}

		public override void Deserialize( GenericReader reader )
		{
			base.Deserialize( reader );
		}
	}
}
