using System;

namespace Server.Items
{
	/// <summary>
	/// Description of EntryStone.
	/// </summary>
	public class EntryStone : Item
	{
		private TourneyController m_Cont;
		
		public EntryStone(TourneyController cont)
		{
			m_Cont = cont;
			Name = "An entry stone";
		}
		
		public override void OnDoubleClick( Mobile from )
        {
			m_Cont.AddParticipant(from as Server.Mobiles.PlayerMobile);
		}
	}
}
