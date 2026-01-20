using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PerforceStreamManager.ViewModels
{
    /// <summary>
    /// View model for displaying a stream rule
    /// </summary>
    public class RuleViewModel : INotifyPropertyChanged
    {
        private string _ruleType = "";
        private string _path = "";
        private string _remapTarget = "";
        private string _sourceStream = "";
        private bool _isInherited;
        private bool _isLocal;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Type of rule: "ignore" or "remap"
        /// </summary>
        public string RuleType
        {
            get => _ruleType;
            set
            {
                if (_ruleType != value)
                {
                    _ruleType = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// The depot path pattern for this rule
        /// </summary>
        public string Path
        {
            get => _path;
            set
            {
                if (_path != value)
                {
                    _path = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Target path for remap rules (empty for ignore rules)
        /// </summary>
        public string RemapTarget
        {
            get => _remapTarget;
            set
            {
                if (_remapTarget != value)
                {
                    _remapTarget = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// The stream path that defined this rule
        /// </summary>
        public string SourceStream
        {
            get => _sourceStream;
            set
            {
                if (_sourceStream != value)
                {
                    _sourceStream = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Indicates if this rule is inherited from a parent stream
        /// </summary>
        public bool IsInherited
        {
            get => _isInherited;
            set
            {
                if (_isInherited != value)
                {
                    _isInherited = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Indicates if this rule is defined locally in the current stream
        /// </summary>
        public bool IsLocal
        {
            get => _isLocal;
            set
            {
                if (_isLocal != value)
                {
                    _isLocal = value;
                    OnPropertyChanged();
                }
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
