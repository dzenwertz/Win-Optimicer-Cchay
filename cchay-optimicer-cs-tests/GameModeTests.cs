using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;
using cchay_optimicer_cs.Services;

namespace cchay_optimicer_cs_tests
{
    public class GameModeTests
    {
        [Fact]
        public void GameModePage_ConstructionAndSettingsLoadPreserveDisabledOptions()
        {
            Exception? failure = null;
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    var page = new cchay_optimicer_cs.Views.GameModePage();
                    var state = GameModeService.State;
                    string before = JsonSerializer.Serialize(state);
                    typeof(cchay_optimicer_cs.Views.GameModePage)
                        .GetMethod("LoadSettingsIntoUI", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                        .Invoke(page, null);
                    Assert.Equal(before, JsonSerializer.Serialize(state));
                    var option = (System.Windows.Controls.CheckBox)page.FindName("CheckStandbyRam");
                    Assert.Equal(state.CleanStandbyRamOnStart, option.IsChecked);
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "La página no terminó de cargar.");
            Assert.Null(failure);
        }

        [Fact]
        public void RecoveryJournal_PreservesAbsentAndCustomRegistryValues()
        {
            var state = new GameModeState { SnapshotVersion = 2 };
            state.RegistrySnapshots.Add(new GameRegistrySnapshot { Path = "test", Name = "Absent", Existed = false });
            state.RegistrySnapshots.Add(new GameRegistrySnapshot { Path = "test", Name = "Custom", Existed = true, Number = -1 });
            var restored = JsonSerializer.Deserialize<GameModeState>(JsonSerializer.Serialize(state))!;
            Assert.False(restored.RegistrySnapshots[0].Existed);
            Assert.Equal(-1, restored.RegistrySnapshots[1].Number);
        }

        private static readonly Regex GuidRegex = new(
            @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$",
            RegexOptions.Compiled);

        [Fact]
        public void PowerSchemeHelper_StandardGuidsAreValid()
        {
            Assert.Matches(GuidRegex, PowerSchemeHelper.BalancedGuid);
            Assert.Matches(GuidRegex, PowerSchemeHelper.HighPerformanceGuid);
            Assert.Matches(GuidRegex, PowerSchemeHelper.UltimatePerformanceTemplateGuid);

            // Balanced plan GUID is standardized across all Windows releases
            Assert.Equal("381b4222-f694-41f0-9685-ff5bb260df2e", PowerSchemeHelper.BalancedGuid);
        }

        [Fact]
        public void GameModeState_DefaultSettingsAreSafeAndOptimal()
        {
            var state = new GameModeState();

            Assert.False(state.IsActive);
            Assert.Null(state.ActivatedAt);
            Assert.True(state.OptimizePowerPlan);
            Assert.False(state.OptimizeCpuScheduling);
            Assert.False(state.OptimizeMultimediaPriority);
            Assert.False(state.PauseSearchIndexing);
            Assert.False(state.PauseSysMain);
            Assert.True(state.DisableGameDvr);
            Assert.False(state.CleanStandbyRamOnStart);
            Assert.True(state.ElevateGameProcessPriority);
        }

        [Fact]
        public void GameModeState_JsonRoundtripSerializationPreservesOriginalValues()
        {
            var original = new GameModeState
            {
                IsActive = true,
                ActivatedAt = DateTime.Now,
                OriginalPowerPlanGuid = "381b4222-f694-41f0-9685-ff5bb260df2e",
                OriginalWin32PrioritySeparation = 2,
                OriginalSystemResponsiveness = 20,
                OriginalNetworkThrottlingIndex = 10,
                OriginalGameDvrEnabled = 1,
                OriginalAppCaptureEnabled = 1,
                WSearchWasRunning = true,
                SysMainWasRunning = true
            };

            string json = JsonSerializer.Serialize(original, new JsonSerializerOptions { WriteIndented = true });
            var deserialized = JsonSerializer.Deserialize<GameModeState>(json);

            Assert.NotNull(deserialized);
            Assert.True(deserialized.IsActive);
            Assert.Equal(original.OriginalPowerPlanGuid, deserialized.OriginalPowerPlanGuid);
            Assert.Equal(original.OriginalWin32PrioritySeparation, deserialized.OriginalWin32PrioritySeparation);
            Assert.Equal(original.OriginalSystemResponsiveness, deserialized.OriginalSystemResponsiveness);
            Assert.Equal(original.OriginalNetworkThrottlingIndex, deserialized.OriginalNetworkThrottlingIndex);
            Assert.Equal(original.OriginalGameDvrEnabled, deserialized.OriginalGameDvrEnabled);
            Assert.Equal(original.OriginalAppCaptureEnabled, deserialized.OriginalAppCaptureEnabled);
            Assert.True(deserialized.WSearchWasRunning);
            Assert.True(deserialized.SysMainWasRunning);
        }

        [Fact]
        public void GameMode_DoesNotDisruptBluetoothOrXboxServices()
        {
            // Game Mode should NOT touch Bluetooth or core Xbox services by default
            // to ensure wireless controllers, headsets, and multiplayer services continue working.
            var state = new GameModeState();
            
            // Check that state doesn't have options disabling bluetooth or xbox
            var properties = typeof(GameModeState).GetProperties();
            foreach (var prop in properties)
            {
                Assert.DoesNotContain("bluetooth", prop.Name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("xbox", prop.Name, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void GameModeService_GetActiveGameStatusDoesNotThrow()
        {
            var status = GameModeService.GetActiveGameStatus();
            // In unit test environment, either a game is running or not, but it must return a valid tuple without throwing
            if (status.IsRunning)
            {
                Assert.False(string.IsNullOrEmpty(status.GameName));
                Assert.True(status.Pid > 0);
            }
            else
            {
                Assert.Empty(status.GameName);
                Assert.Equal(0, status.Pid);
            }
        }

        [Fact]
        public void GameModeOperationResult_TracksAppliedAndErrors()
        {
            var res = new GameModeOperationResult
            {
                Success = true,
                Message = "Test"
            };
            res.AppliedOptimizations.Add("Test tweak");

            Assert.True(res.Success);
            Assert.Single(res.AppliedOptimizations);
            Assert.Empty(res.Errors);
        }

        [Fact]
        public async Task TweakService_CoolQuietLaptopExistsAndIsSafe()
        {
            var tweaks = await TweakService.GetTweaksAsync();
            var coolQuiet = tweaks.Find(t => t.Key == "cool-quiet-laptop");
            Assert.NotNull(coolQuiet);
            Assert.Equal("safe", coolQuiet.Risk);
        }

        [Fact]
        public void JsonStorageHelper_MigrateLegacyData_SafeExecution()
        {
            // Verify migration does not throw regardless of whether folders exist or not
            var exception = Record.Exception(() => JsonStorageHelper.MigrateLegacyData());
            Assert.Null(exception);
        }
    }
}
