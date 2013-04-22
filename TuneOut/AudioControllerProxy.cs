using System;
using System.ComponentModel;

namespace TuneOut
{
	public sealed class AudioControllerProxy : INotifyPropertyChanged, IDisposable
	{
		public void Dispose()
		{
			IsSynchronizingValues = false;
		}

		private bool _IsHandlingEvents;

		public bool IsSynchronizingValues
		{
			get
			{
				return _IsHandlingEvents;
			}
			set
			{
				if (_IsHandlingEvents && value == false)
				{
					Audio.AudioController.Default.PropertyChanged -= Default_PropertyChanged;
					_IsHandlingEvents = false;
				}
				else if (!_IsHandlingEvents && value == true)
				{
					Audio.AudioController.Default.PropertyChanged += Default_PropertyChanged;
					OnPropertyChanged("VolumeDisplayLabel");
					_IsHandlingEvents = true;
				}
			}
		}

		private void Default_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "Volume" || e.PropertyName == "IsMuted")
			{
				OnPropertyChanged("VolumeDisplayLabel");
			}
		}

		public Audio.AudioController Controller
		{
			get
			{
				return Audio.AudioController.Default;
			}
		}

		public string VolumeDisplayLabel
		{
			get
			{
				if (Controller.IsMuted)
				{
					return AppData.LocalizationManager.GetString("TransportControls/Volume/Muted");
				}
				else
				{
					return string.Format(AppData.LocalizationManager.GetString("TransportControls/Volume/Active_F"), (int)Math.Round(Controller.Volume));
				}
			}
		}

		public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged(string propertyName)
		{
			PropertyChangedEventHandler h = PropertyChanged;
			if (h != null)
			{
				h(this, new PropertyChangedEventArgs(propertyName));
			}
		}
	}
}