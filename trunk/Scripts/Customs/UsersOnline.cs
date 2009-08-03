using System;
using System.IO;
using Server.Network;

namespace Server.Scripts.Customs
{
	/// <summary>
	/// Description of UsersOnline.
	/// </summary>
	public class UsersOnline
	{
		public static void Initialize()
		{
			Timer.DelayCall( TimeSpan.FromSeconds( 20.0 ), TimeSpan.FromMinutes(5), new TimerCallback( Begin ) );
		}
	
		public static void Begin()
		{
			using ( StreamWriter op = new StreamWriter( "online.txt" ) )
			{
				op.Write(NetState.Instances.Count);
			}
		}
	}
}
