using System;
using System.Collections.Generic;

namespace Server.Items
{
	/// <summary>
	/// Description of TeamBase.
	/// </summary>
	public class TeamBase : Item
	{
		private int m_Team;
		
		public TeamBase(int team, TourneyController cont) : base( 0x1BC4 )
		{
			Movable = false;
			Visible = false;
			
			m_Team = team;
			List<TeamBase> bases = cont.Bases;
			if(bases.Count < team)
			{
				while(bases.Count < team)
					bases.Add(null);
				bases.Add(this);
			} else
			{
				TeamBase old = bases[team];
				bases[team] = this;
				
				if(old != null)
					old.Delete();
			}
			Name = team == 0 ? "Home location" : String.Format("Team {0} Base", team);
		}
	}
}
