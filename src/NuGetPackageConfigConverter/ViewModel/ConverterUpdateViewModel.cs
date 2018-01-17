using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NuGetPackageConfigConverter
{
    public class ConverterUpdateViewModel : INotifyPropertyChanged
    {
        private string _status;
        private int _total;
        private int _count;
        private bool _isIndeterminate;
        private string _phase;
        private string _log;

        public ConverterUpdateViewModel()
        {
            _status = "Calculating...";
            _isIndeterminate = true;
            _count = 0;
            _total = 0;
            _phase = "Loading program";
        }

        public string Status
        {
            get { return _status; }
            set { UpdateProperty(ref _status, value); }
        }

        public string Log
        {
            get { return _log; }
            set { UpdateProperty(ref _log, value); }
        }
        public string Phase
        {
            get { return _phase; }
            set { UpdateProperty(ref _phase, value); }
        }

        public int Total
        {
            get { return _total; }
            set { UpdateProperty(ref _total, value); }
        }

        public int Count
        {
            get { return _count; }
            set { UpdateProperty(ref _count, value); }
        }

        public bool IsIndeterminate
        {
            get { return _isIndeterminate; }
            set { UpdateProperty(ref _isIndeterminate, value); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void UpdateProperty<T>(ref T field, T value, IEqualityComparer<T> comparer = null, [CallerMemberName]string name = null)
        {
            var equalityComparer = comparer ?? EqualityComparer<T>.Default;

            if (equalityComparer.Equals(field, value))
            {
                return;
            }

            field = value;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
