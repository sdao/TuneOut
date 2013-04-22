using Windows.Networking.Connectivity;

namespace TuneOut.AppData
{
	/// <summary>
	/// A helper for determining whether to connect to the Internet. Singleton.
	/// </summary>
	internal class NetworkStatusManager
	{
		private static NetworkStatusManager _Default = new NetworkStatusManager(); // Eager load

		/// <summary>
		/// The singleton.
		/// </summary>
		internal static NetworkStatusManager Default
		{
			get
			{
				return _Default;
			}
		}

		private bool _internetConnected = false;
		private bool _internetThrottled = false;

		/// <summary>
		/// Creates a new NetworkStatusManager.
		/// </summary>
		private NetworkStatusManager()
		{
			NetworkInformation_NetworkStatusChanged(null);
			NetworkInformation.NetworkStatusChanged += NetworkInformation_NetworkStatusChanged;
		}

		private void NetworkInformation_NetworkStatusChanged(object sender)
		{
			ConnectionProfile internetConnectionProfile = NetworkInformation.GetInternetConnectionProfile();

			if (internetConnectionProfile == null)
			{
				// No connection
				_internetConnected = false;
				_internetThrottled = false;
			}
			else
			{
				var connectionCost = internetConnectionProfile.GetConnectionCost();
				var connectionLevel = internetConnectionProfile.GetNetworkConnectivityLevel();

				if (connectionLevel != NetworkConnectivityLevel.InternetAccess)
				{
					// No connection
					_internetConnected = false;
					_internetThrottled = false;
				}
				else if (connectionCost.Roaming || connectionCost.OverDataLimit)
				{
					// Connection but throttled
					_internetConnected = true;
					_internetThrottled = true;
				}
				else
				{
					// Full connection
					_internetConnected = true;
					_internetThrottled = false;
				}
			}
		}

		/// <summary>
		/// Gets whether Internet is available at all.
		/// </summary>
		internal bool IsInternetAvailable
		{
			get
			{
				return _internetConnected;
			}
		}

		/// <summary>
		/// Gets whether background downloads of optional data should occur.
		/// </summary>
		internal bool IsUnlimitedInternetAvailable
		{
			get
			{
				return _internetConnected && (!_internetThrottled || Settings.IsInternetPolicyIgnored);
			}
		}
	}
}