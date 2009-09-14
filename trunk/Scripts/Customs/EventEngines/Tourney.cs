using System;
using System.Collections.Generic;
using Server;
using Server.Gumps;
using Server.Network;
using Server.Mobiles;
using System.Collections;
using Server.Targeting;
using Server.Regions;
using Server.Engines.XmlSpawner2;

namespace Server.Items
{
	public class TourneyItem
	{
		private int m_ID;
		private String m_Name;
		private Type m_ItemType;
		private int m_Hue;
		private object[] m_Params;
		
		public Item item;
		
		public int Hue { get { return m_Hue; } }
		public int ID { get { return m_ID; } }
		public string Name { get { return m_Name; } }
		public Type ItemType { get { return m_ItemType; } }
		
		public TourneyItem( int id, String name, Type itemType, params object[] a)
		{
			m_ID = id;
			m_Name = name;
			m_ItemType = itemType;
			m_Params = a;
		}
		
		public Item Make()
		{
			object ret = Activator.CreateInstance(m_ItemType, m_Params);
			item = ret as Item;
			return item;
		}		
	}
	
	public class TourneyEntry
	{
		private bool m_Remove;
		
		public bool Remove {
			get { return m_Remove; }
			set { m_Remove = value; }
		}
		
		private int m_Team;
		
		public int Team {
			get { return m_Team; }
			set { m_Team = value; }
		}
		
		private int m_Param1;
		
		public int Param1 {
			get { return m_Param1; }
			set { m_Param1 = value; }
		}
		private int m_Param2;
		
		public int Param2 {
			get { return m_Param2; }
			set { m_Param2 = value; }
		}
		private int m_Param3;
		
		public int Param3 {
			get { return m_Param3; }
			set { m_Param3 = value; }
		}
		
		public TourneyEntry()
		{
			m_Remove = false;
		}
	}
	
	public enum GameType
	{
		FreezeTag,
		CTF
	}
	
	public class TourneyController : BaseChallengeGame
    {
    	private bool m_KeepItems;
    	private GameType m_GameType;
    	private int m_Teams;
    	private int m_Round, m_Rounds;
    	private List<TeamBase> bases = new List<TeamBase>();
    	private bool m_GameLocked, m_GameInProgress;
    	
		public List<TeamBase> Bases {
			get { return bases; }
		}
    	
    	[CommandProperty( AccessLevel.GameMaster )]
        public override bool GameCompleted { get{ return !m_GameInProgress && m_GameLocked; } }
    	
    	public override int TotalPurse { get { return 0; } set { } }

        public override int EntryFee { get { return 0; } set {  } }
        
        public override int ArenaSize { get{ return 0; } set { } }
        
        public override Mobile Challenger { get{ return null; } set { } }

        public override bool GameLocked { get{ return m_GameLocked; } set { m_GameLocked = value; }}

        public override bool GameInProgress { get{ return m_GameInProgress; } set { m_GameInProgress = value; }}
    	
		public GameType GameType {
			get { return m_GameType; }
		}
    	private Dictionary<PlayerMobile, TourneyEntry> m_Participants = new Dictionary<PlayerMobile, TourneyEntry>();
    	private Dictionary<PlayerMobile, Container> m_StolenBackpack = new Dictionary<PlayerMobile, Container>();
    	
    	private TourneyItem[] TItems;
    		
    	public override ArrayList Participants { get { return new ArrayList(m_Participants.Keys); } set {} }
    	public bool KeepItem { get { return m_KeepItems; } set { m_KeepItems = value; } }
    	
    	public TourneyController( Serial serial ) : base( serial )
        {
        }
    	
    	public TourneyController() : base( 0x1414 )
    	{
            Movable = false;
            Hue = 33;
			Name = "Tournament Controller";
			Visible = false;
			AllowPoints = false;
			KeepItems = true;
			
			ChallengeGameRegion reg = Region.Find(Location, Map) as ChallengeGameRegion;
			if(reg == null || reg.ChallengeGame != null || !reg.ChallengeGame.Deleted)
			{
				Delete();
			} else
			{
				reg.ChallengeGame = this;
			}
			TItems = new TourneyItem[]
			{
	    		new TourneyItem( 0, "Entry Stone", typeof(EntryStone), this),
	    		new TourneyItem( 0, "Home location", typeof(TeamBase), 0, this),
	    		new TourneyItem( 1, "Team 1 Base", typeof(TeamBase), 1, this),
	    		new TourneyItem( 2, "Team 2 Base", typeof(TeamBase), 2, this)
			};
        }
    	
    	public override bool InsuranceIsFree(Mobile from, Mobile awardto)
        {
            return m_KeepItems;
        }
    	
    	public override void OnDoubleClick( Mobile from )
        {
    		if(from.AccessLevel < AccessLevel.GameMaster)
    			return;
    		
    		foreach(TourneyItem tItem in TItems)
    		{
    			if(tItem.item == null)
    			{
    				if(tItem.Make() == null)
    				{
    					from.SendMessage("Unable to generate items");
    					return;
    				}
    				from.AddToBackpack(tItem.item);
    			}
    		}
        }
    	
    	public override void OnTick()
		{
    		
    	}
    	
    	public override void OnKillPlayer(Mobile killer, Mobile killed)
        {
    		
    	}
    	
    	private void Refresh(Mobile m)
    	{
		    List<StatMod> toRemove = new List<StatMod>();
			foreach(StatMod mod in m.StatMods)
			{
				if(mod.Name.Contains("[Magic]"))
					toRemove.Add(mod);
			}
			foreach(StatMod mod in toRemove)
			{
				m.RemoveStatMod(mod.Name);
			}
			
			m.Hits = m.HitsMax;
			m.Stam = m.StamMax;
			m.Mana = m.ManaMax;
			
			m.Poison = null;
			m.Combatant = null;
    	}
    	
    	public override void OnPlayerKilled(Mobile killer, Mobile killed)
        {
    		switch(m_GameType)
    		{
    			case GameType.FreezeTag:
    				killed.Frozen = true;
    				killed.SolidHueOverride = 1156; //Frozen colour
    				Refresh(killed);
    				killed.Blessed = true;
    				killed.SendMessage("You have been frozen. Wait for your team to rescue you.");
    				break;
    			case GameType.CTF:
    				Timer.DelayCall(TimeSpan.FromSeconds(20), new TimerStateCallback<RespawnEntry>(TimedRespawn), new RespawnEntry(RespawnLocation.Base, killed));
    				break;
    		}
    		CheckForGameEnd();
    	}
    	
    	public override void CheckForGameEnd()
    	{
    		//TODO Check time
    		
    		//Check game related ending
    		bool end = false;
    		switch(m_GameType)
    		{
    			case GameType.FreezeTag:
    				end = FreezeTagEnd();
    				break;
    		}
    		
    		if(!end)
    			return;
    		
    		//Game has ended
    		
    		Timer.DelayCall(TimeSpan.FromSeconds(10), new TimerCallback(StartRound));
    	}
    	
    	private void StartRound()
    	{
    		if(m_Round++ >= m_Rounds)
    		{
    			GameBroadcast("Thats the end! Thanks for playing.");
    			EndGame();
    			return;
    		}
    		GameBroadcast(String.Format("Starting round {0} of {1}.", m_Round, m_Rounds));
    		
    		ResetPlayers();
    		GameBroadcast("Ready? Starting in 10 seconds.");
    		Timer.DelayCall(TimeSpan.FromSeconds(10.0), new TimerCallback(UnfreezePlayers));
    	}
    	
    	public void UnfreezePlayers()
    	{
    		foreach( KeyValuePair<PlayerMobile, TourneyEntry> item in m_Participants)
    		{
    			item.Key.Frozen = false;
    		}
    		GameBroadcast("Go Go Go!");
    	}
    	
    	private void ResetPlayers()
    	{
    		foreach( KeyValuePair<PlayerMobile, TourneyEntry> item in m_Participants)
    		{
    			PlayerMobile m = item.Key;
    			Refresh(m);
    			m.Frozen = true;
    			
    			//Undress
    			List<Item> dress = new List<Item>(m.Items);
    			foreach(Item item2 in dress)
    			{
    				if(item2 == m.Backpack)
    					continue;
    				DeathMoveResult res = m.GetParentMoveResultFor(item2);
    				if(res == DeathMoveResult.MoveToBackpack || res == DeathMoveResult.MoveToCorpse)
    				{
    					m.Backpack.AddItem(item2);
    				}
    			}
    			
    			//Clear backapck
    			if(m_StolenBackpack.ContainsKey(m))
    			{
    				m.Backpack.Delete();
    			} else
    			{
    				m.BankBox.DropItem(m.Backpack);
    			}
    			
    			//New backpack
			    Backpack pack = new Backpack();
				pack.Movable = false;
				m.AddItem(pack);
				
				//Supply items
				AddItems(pack);
				
				//Robe
				m.EquipItem(new DeathRobe());
				
				//Team colour
				m.SolidHueOverride = item.Value.Team;
				
				//Move to base
				TeamBase bas = bases[item.Value.Team];
				if(bas != null)
				{
					m.MoveToWorld(bas.Location, bas.Map);
				}
    		}
    	}
    		
    	private void AddItems(Container pack)
    	{
    		pack.DropItem(new BagOfReagents());
    		pack.DropItem(new Spellbook(ulong.MaxValue));
    		
    		BaseWeapon weaps = new Halberd();
    		weaps.Quality = WeaponQuality.Exceptional;
    		pack.DropItem(weaps);
    		
    		weaps = new Katana();
    		weaps.Quality = WeaponQuality.Exceptional;
    		pack.DropItem(weaps);
    		
    		weaps = new ShortSpear();
    		weaps.Quality = WeaponQuality.Exceptional;
    		pack.DropItem(weaps);
    		
    		weaps = new WarMace();
    		weaps.Quality = WeaponQuality.Exceptional;
    		pack.DropItem(weaps);
    		
    		pack.DropItem(new GreaterPoisonPotion());
    		pack.DropItem(new GreaterPoisonPotion());
    		
    		pack.DropItem(new GreaterHealPotion());
    		pack.DropItem(new GreaterHealPotion());
    		pack.DropItem(new GreaterHealPotion());
    		pack.DropItem(new GreaterHealPotion());
    		
    		pack.DropItem(new GreaterCurePotion());
    		pack.DropItem(new GreaterCurePotion());
    		pack.DropItem(new GreaterCurePotion());
    		
    		pack.DropItem(new TotalRefreshPotion());
    		pack.DropItem(new TotalRefreshPotion());
    		
    		pack.DropItem(new GreaterExplosionPotion());
    		pack.DropItem(new GreaterExplosionPotion());
    		pack.DropItem(new GreaterExplosionPotion());
    		
    		pack.DropItem(new Bandage(20));
    		
    		for(int i = 0; i < 3; i++)
    		{
    			TrapableContainer cont = new Pouch();
    			cont.TrapType = TrapType.MagicTrap;
    			cont.TrapPower = 1;
    			cont.TrapLevel = 0;
    			pack.DropItem(cont);
    		}
    	}
    	
    	private bool FreezeTagEnd()
    	{
    		bool[] teams = new bool[m_Teams+1];
    		
    		for(int i = 0; i < teams.Length; i++)
    			teams[i] = false;
    		    		
    		foreach( KeyValuePair<PlayerMobile, TourneyEntry> item in m_Participants)
    		{
    			TourneyEntry entry = item.Value;
    			int team = entry.Team;
    			if(team > m_Teams || team < 1)
    				continue;
    			if(!item.Key.Frozen)
    				teams[team] = true;
    		}
    		
    		int alive = 0;
    		for(int i = 1; i < teams.Length; i++)
    			if(teams[i])
    				alive++;
    		if(alive == 1)
    		{
    			//Find winning team
    			for(int i = 1; i < teams.Length; i++)
    			{
    				if(teams[i])
    				{
    					GameBroadcast(String.Format("Team {0} has won the round!", i));
    					break;
    				}
    			}
    		}
    		
    		return alive < 2;
    	}
    	
    	private void TimedRespawn(RespawnEntry ent)
    	{
    		
    	}
    	
    	sealed class RespawnEntry
    	{
    		public RespawnLocation loc;
    		public Mobile mob;
    		
    		public RespawnEntry(RespawnLocation loc, Mobile mob)
    		{
    			this.loc = loc;
    			this.mob = mob;
    		}
    	}
    	
    	private enum RespawnLocation
    	{
    		Anywhere,
    		Base,
    		Corpse
    	}
    	
    	public void AddParticipant(PlayerMobile from)
    	{
    		if (from == null)
    			return;
    		
    		if(m_Participants.ContainsKey(from))
    		{
    			if(m_Participants[from].Remove)
    			{
    				m_Participants[from].Remove = false;
    				from.SendMessage("You have elected to remain in the game.");
    			} else
    			{
	    			m_Participants[from].Remove = true;
	    			from.SendMessage("You have been removed from the game.");
    			}
    		} else
    		{
    			m_Participants.Add(from, new TourneyEntry());
    			from.SendMessage("You have been added to the game.");
    		}
    	}
    	
    	public override bool AreChallengers(Mobile from, Mobile target)
        {
    		return false;
    	}
    	
    	public override bool AreTeamMembers(Mobile from, Mobile target)
        {
    		return false;
    	}   	
    }
}