using System;
using Server.Network;

namespace Server
{
	public class CurrentExpansion
	{
		private static readonly Expansion Expansion = Expansion.None;

		public static void Configure()
		{
			Core.Expansion = Expansion;

			bool Enabled = true;

			Mobile.InsuranceEnabled = false;
			ObjectPropertyList.Enabled = Enabled;
			Mobile.VisibleDamageType = Enabled ? VisibleDamageType.Related : VisibleDamageType.None;
			Mobile.GuildClickMessage = !Enabled;
			Mobile.AsciiClickMessage = !Enabled;

			//ExpansionInfo.Table[0] = new ExpansionInfo( 0, "Mondain's Legacy", new ClientVersion( "5.0.0a" ),	0x82DF, 0x008, 0x2E0 );

			if ( Enabled )
			{
				//AOS.DisableStatInfluences();

				if ( ObjectPropertyList.Enabled )
					PacketHandlers.SingleClickProps = true; // single click for everything is overriden to check object property list
			}
		}
	}
}
