using NUnit.Framework;
using PerforceStreamManager.ViewModels;
using PerforceStreamManager.Models;
using PerforceStreamManager.Services;
using System.Collections.Generic;
using System.Linq;

namespace PerforceStreamManager.Tests
{
    [TestFixture]
    public class MainViewModelTests
    {
        private MainViewModel _viewModel;
        private P4Service _p4Service;
        private SnapshotService _snapshotService;
        private SettingsService _settingsService;
        private LoggingService _loggingService;

        [SetUp]
        public void SetUp()
        {
            // Initialize services for testing
            _loggingService = new LoggingService();
            _p4Service = new P4Service(_loggingService);
            _settingsService = new SettingsService(_loggingService);
            _snapshotService = new SnapshotService(_p4Service, _loggingService);
            
            // Create MainViewModel with dependency injection
            _viewModel = new MainViewModel(_p4Service, _snapshotService, _settingsService);
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up and dispose services
            _p4Service?.Dispose();
        }

        [Test]
        public void RefreshRuleDisplay_SeparatesRemapAndIgnoreRules()
        {
            // Arrange
            var mockStream = new StreamNode
            {
                Path = "//depot/main",
                Name = "main",
                Parent = null,
                LocalRules = new List<StreamRule>
                {
                    new StreamRule
                    {
                        Type = "remap",
                        Path = "lib/...",
                        RemapTarget = "//shared/lib/...",
                        SourceStream = "//depot/main"
                    },
                    new StreamRule
                    {
                        Type = "ignore",
                        Path = "temp/...",
                        RemapTarget = "",
                        SourceStream = "//depot/main"
                    }
                }
            };
            
            _viewModel.CurrentViewMode = RuleViewMode.Local;

            // Act
            _viewModel.SelectedStream = mockStream;

            // Assert
            Assert.That(_viewModel.DisplayedRemapRules.Count, Is.EqualTo(1), "Should have 1 remap rule");
            Assert.That(_viewModel.DisplayedIgnoreRules.Count, Is.EqualTo(1), "Should have 1 ignore rule");
            Assert.That(_viewModel.DisplayedRemapRules[0].RuleType, Is.EqualTo("remap"));
            Assert.That(_viewModel.DisplayedIgnoreRules[0].RuleType, Is.EqualTo("ignore"));
        }

        [Test]
        public void RefreshRuleDisplay_RemapRulesOnlyInRemapCollection()
        {
            // Arrange
            var mockStream = new StreamNode
            {
                Path = "//depot/main",
                Name = "main",
                Parent = null,
                LocalRules = new List<StreamRule>
                {
                    new StreamRule
                    {
                        Type = "remap",
                        Path = "lib/...",
                        RemapTarget = "//shared/lib/...",
                        SourceStream = "//depot/main"
                    },
                    new StreamRule
                    {
                        Type = "remap",
                        Path = "tools/...",
                        RemapTarget = "//shared/tools/...",
                        SourceStream = "//depot/main"
                    }
                }
            };
            
            _viewModel.CurrentViewMode = RuleViewMode.Local;

            // Act
            _viewModel.SelectedStream = mockStream;

            // Assert
            Assert.That(_viewModel.DisplayedRemapRules.Count, Is.EqualTo(2), "Should have 2 remap rules");
            Assert.That(_viewModel.DisplayedIgnoreRules.Count, Is.EqualTo(0), "Should have 0 ignore rules");
            Assert.That(_viewModel.DisplayedRemapRules.All(r => r.RuleType.Equals("remap", System.StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        [Test]
        public void RefreshRuleDisplay_IgnoreRulesOnlyInIgnoreCollection()
        {
            // Arrange
            var mockStream = new StreamNode
            {
                Path = "//depot/main",
                Name = "main",
                Parent = null,
                LocalRules = new List<StreamRule>
                {
                    new StreamRule
                    {
                        Type = "ignore",
                        Path = "temp/...",
                        RemapTarget = "",
                        SourceStream = "//depot/main"
                    },
                    new StreamRule
                    {
                        Type = "ignore",
                        Path = "build/...",
                        RemapTarget = "",
                        SourceStream = "//depot/main"
                    }
                }
            };
            
            _viewModel.CurrentViewMode = RuleViewMode.Local;

            // Act
            _viewModel.SelectedStream = mockStream;

            // Assert
            Assert.That(_viewModel.DisplayedRemapRules.Count, Is.EqualTo(0), "Should have 0 remap rules");
            Assert.That(_viewModel.DisplayedIgnoreRules.Count, Is.EqualTo(2), "Should have 2 ignore rules");
            Assert.That(_viewModel.DisplayedIgnoreRules.All(r => r.RuleType.Equals("ignore", System.StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        [Test]
        public void RefreshRuleDisplay_HandlesEmptyCollections()
        {
            // Arrange - stream with only remap rules
            var streamWithOnlyRemap = new StreamNode
            {
                Path = "//depot/main",
                Name = "main",
                Parent = null,
                LocalRules = new List<StreamRule>
                {
                    new StreamRule
                    {
                        Type = "remap",
                        Path = "lib/...",
                        RemapTarget = "//shared/lib/...",
                        SourceStream = "//depot/main"
                    }
                }
            };
            
            _viewModel.CurrentViewMode = RuleViewMode.Local;

            // Act
            _viewModel.SelectedStream = streamWithOnlyRemap;

            // Assert
            Assert.That(_viewModel.DisplayedRemapRules.Count, Is.EqualTo(1), "Should have 1 remap rule");
            Assert.That(_viewModel.DisplayedIgnoreRules.Count, Is.EqualTo(0), "Ignore collection should be empty");

            // Arrange - stream with only ignore rules
            var streamWithOnlyIgnore = new StreamNode
            {
                Path = "//depot/main",
                Name = "main",
                Parent = null,
                LocalRules = new List<StreamRule>
                {
                    new StreamRule
                    {
                        Type = "ignore",
                        Path = "temp/...",
                        RemapTarget = "",
                        SourceStream = "//depot/main"
                    }
                }
            };

            // Act
            _viewModel.SelectedStream = streamWithOnlyIgnore;

            // Assert
            Assert.That(_viewModel.DisplayedRemapRules.Count, Is.EqualTo(0), "Remap collection should be empty");
            Assert.That(_viewModel.DisplayedIgnoreRules.Count, Is.EqualTo(1), "Should have 1 ignore rule");
        }

        [Test]
        public void RefreshRuleDisplay_CaseInsensitiveRuleTypeSorting()
        {
            // Arrange
            var mockStream = new StreamNode
            {
                Path = "//depot/main",
                Name = "main",
                Parent = null,
                LocalRules = new List<StreamRule>
                {
                    new StreamRule
                    {
                        Type = "REMAP",
                        Path = "lib/...",
                        RemapTarget = "//shared/lib/...",
                        SourceStream = "//depot/main"
                    },
                    new StreamRule
                    {
                        Type = "Remap",
                        Path = "tools/...",
                        RemapTarget = "//shared/tools/...",
                        SourceStream = "//depot/main"
                    },
                    new StreamRule
                    {
                        Type = "IGNORE",
                        Path = "temp/...",
                        RemapTarget = "",
                        SourceStream = "//depot/main"
                    },
                    new StreamRule
                    {
                        Type = "Ignore",
                        Path = "build/...",
                        RemapTarget = "",
                        SourceStream = "//depot/main"
                    }
                }
            };
            
            _viewModel.CurrentViewMode = RuleViewMode.Local;

            // Act
            _viewModel.SelectedStream = mockStream;

            // Assert
            Assert.That(_viewModel.DisplayedRemapRules.Count, Is.EqualTo(2), "Should have 2 remap rules (case-insensitive)");
            Assert.That(_viewModel.DisplayedIgnoreRules.Count, Is.EqualTo(2), "Should have 2 ignore rules (case-insensitive)");
        }

        [Test]
        public void RefreshRuleDisplay_WithLocalViewMode_ShowsOnlyLocalRules()
        {
            // Arrange
            var parentStream = new StreamNode
            {
                Path = "//depot/parent",
                Name = "parent",
                Parent = null,
                LocalRules = new List<StreamRule>
                {
                    new StreamRule
                    {
                        Type = "ignore",
                        Path = "temp/...",
                        RemapTarget = "",
                        SourceStream = "//depot/parent"
                    }
                }
            };

            var mockStream = new StreamNode
            {
                Path = "//depot/main",
                Name = "main",
                Parent = parentStream,
                LocalRules = new List<StreamRule>
                {
                    new StreamRule
                    {
                        Type = "remap",
                        Path = "lib/...",
                        RemapTarget = "//shared/lib/...",
                        SourceStream = "//depot/main"
                    }
                }
            };
            
            _viewModel.CurrentViewMode = RuleViewMode.Local;

            // Act
            _viewModel.SelectedStream = mockStream;

            // Assert - Only local rules should be displayed
            Assert.That(_viewModel.DisplayedRemapRules.Count, Is.EqualTo(1), "Should have 1 local remap rule");
            Assert.That(_viewModel.DisplayedIgnoreRules.Count, Is.EqualTo(0), "Should have 0 local ignore rules");
        }

        [Test]
        public void RefreshRuleDisplay_WithInheritedViewMode_ShowsOnlyInheritedRules()
        {
            // Arrange
            var parentStream = new StreamNode
            {
                Path = "//depot/parent",
                Name = "parent",
                Parent = null,
                LocalRules = new List<StreamRule>
                {
                    new StreamRule
                    {
                        Type = "ignore",
                        Path = "temp/...",
                        RemapTarget = "",
                        SourceStream = "//depot/parent"
                    }
                }
            };

            var mockStream = new StreamNode
            {
                Path = "//depot/main",
                Name = "main",
                Parent = parentStream,
                LocalRules = new List<StreamRule>
                {
                    new StreamRule
                    {
                        Type = "remap",
                        Path = "lib/...",
                        RemapTarget = "//shared/lib/...",
                        SourceStream = "//depot/main"
                    }
                }
            };
            
            _viewModel.CurrentViewMode = RuleViewMode.Inherited;

            // Act
            _viewModel.SelectedStream = mockStream;

            // Assert - Only inherited rules should be displayed
            Assert.That(_viewModel.DisplayedRemapRules.Count, Is.EqualTo(0), "Should have 0 inherited remap rules");
            Assert.That(_viewModel.DisplayedIgnoreRules.Count, Is.EqualTo(1), "Should have 1 inherited ignore rule");
        }

        [Test]
        public void RefreshRuleDisplay_WithAllViewMode_ShowsBothLocalAndInherited()
        {
            // Arrange
            var parentStream = new StreamNode
            {
                Path = "//depot/parent",
                Name = "parent",
                Parent = null,
                LocalRules = new List<StreamRule>
                {
                    new StreamRule
                    {
                        Type = "ignore",
                        Path = "temp/...",
                        RemapTarget = "",
                        SourceStream = "//depot/parent"
                    },
                    new StreamRule
                    {
                        Type = "remap",
                        Path = "tools/...",
                        RemapTarget = "//shared/tools/...",
                        SourceStream = "//depot/parent"
                    }
                }
            };

            var mockStream = new StreamNode
            {
                Path = "//depot/main",
                Name = "main",
                Parent = parentStream,
                LocalRules = new List<StreamRule>
                {
                    new StreamRule
                    {
                        Type = "remap",
                        Path = "lib/...",
                        RemapTarget = "//shared/lib/...",
                        SourceStream = "//depot/main"
                    }
                }
            };
            
            _viewModel.CurrentViewMode = RuleViewMode.All;

            // Act
            _viewModel.SelectedStream = mockStream;

            // Assert - All rules (local + inherited) should be displayed
            Assert.That(_viewModel.DisplayedRemapRules.Count, Is.EqualTo(2), "Should have 2 total remap rules (1 local + 1 inherited)");
            Assert.That(_viewModel.DisplayedIgnoreRules.Count, Is.EqualTo(1), "Should have 1 total ignore rule (inherited)");
        }

        [Test]
        public void RefreshRuleDisplay_WithNullStream_ClearsCollections()
        {
            // Arrange - First set a stream with rules
            var mockStream = new StreamNode
            {
                Path = "//depot/main",
                Name = "main",
                Parent = null,
                LocalRules = new List<StreamRule>
                {
                    new StreamRule
                    {
                        Type = "remap",
                        Path = "lib/...",
                        RemapTarget = "//shared/lib/...",
                        SourceStream = "//depot/main"
                    }
                }
            };
            
            _viewModel.SelectedStream = mockStream;
            Assert.That(_viewModel.DisplayedRemapRules.Count, Is.EqualTo(1), "Should have 1 remap rule initially");

            // Act - Set stream to null
            _viewModel.SelectedStream = null;

            // Assert - Collections should be cleared
            Assert.That(_viewModel.DisplayedRemapRules.Count, Is.EqualTo(0), "Remap collection should be empty");
            Assert.That(_viewModel.DisplayedIgnoreRules.Count, Is.EqualTo(0), "Ignore collection should be empty");
        }
    }
}
