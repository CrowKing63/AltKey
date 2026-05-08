using System.Text.Json;
using System.Threading;
using AltKey.Models;
using AltKey.Services;
using AltKey.ViewModels;

namespace AltKey.Tests;

public class LayoutEditorViewModelTests
{
    [Fact]
    public void Save_compact_row_height_writes_same_height_to_all_columns_in_row_band()
    {
        RunSta(() =>
        {
            var repo = new FakeLayoutRepository();
            repo.Seed("shared-row", CreateTwoColumnLayout());

            var vm = new LayoutEditorViewModel(repo, new ConfigService());
            vm.LoadLayout("shared-row");

            var firstRow = vm.Columns[0].Rows[0];
            vm.SetRowCompactHeightCommand.Execute(firstRow);
            vm.SaveCommand.Execute(null);

            Assert.NotNull(repo.LastSavedConfig);
            var firstBandHeights = repo.LastSavedConfig!.Columns!
                .Select(column => column.Rows![0].Keys[0].Height)
                .ToArray();

            Assert.All(firstBandHeights, height => Assert.Equal(EditableKeySlotVm.CompactHeightRatio, height));
            Assert.Equal(EditableKeySlotVm.DefaultHeightRatio, repo.LastSavedConfig.Columns![0].Rows![1].Keys[0].Height);
        });
    }

    [Fact]
    public void LoadLayout_restores_compact_height_to_all_rows_with_same_index()
    {
        RunSta(() =>
        {
            var repo = new FakeLayoutRepository();
            repo.Seed("shared-row", CreateTwoColumnLayout(firstRowHeight: EditableKeySlotVm.CompactHeightRatio));

            var vm = new LayoutEditorViewModel(repo, new ConfigService());
            vm.LoadLayout("shared-row");

            Assert.Equal("낮음", vm.Columns[0].Rows[0].HeightPresetLabel);
            Assert.Equal("낮음", vm.Columns[1].Rows[0].HeightPresetLabel);
            Assert.Equal(EditableKeySlotVm.CompactHeightRatio, vm.Columns[0].Rows[0].Keys[0].EditHeight);
            Assert.Equal(EditableKeySlotVm.CompactHeightRatio, vm.Columns[1].Rows[0].Keys[0].EditHeight);
            Assert.Equal("기본", vm.Columns[0].Rows[1].HeightPresetLabel);
        });
    }

    [Fact]
    public void Save_preserves_function_action_and_labels()
    {
        RunSta(() =>
        {
            var repo = new FakeLayoutRepository();
            repo.Seed("fn-layout", new LayoutConfig("fn-layout", null,
            [
                new KeyColumn(0,
                [
                    new KeyRow([
                        new KeySlot("A", null, new SendKeyAction("VK_A"), 1.0, 1.0, "", 0.0, "a", null,
                            new SendKeyAction("VK_F1"), "F1", null, "f1", null)
                    ])
                ])
            ]));

            var vm = new LayoutEditorViewModel(repo, new ConfigService());
            vm.LoadLayout("fn-layout");
            vm.SaveCommand.Execute(null);

            var savedKey = repo.LastSavedConfig!.Columns![0].Rows![0].Keys[0];
            Assert.Equal("F1", savedKey.FunctionLabel);
            Assert.Equal("f1", savedKey.FunctionEnglishLabel);
            Assert.IsType<SendKeyAction>(savedKey.FunctionAction);
        });
    }

    [Fact]
    public void Changing_key_to_function_toggle_clears_and_locks_function_overrides()
    {
        RunSta(() =>
        {
            var repo = new FakeLayoutRepository();
            repo.Seed("fn-lock", new LayoutConfig("fn-lock", null,
            [
                new KeyColumn(0,
                [
                    new KeyRow([
                        new KeySlot("A", null, new SendKeyAction("VK_A"), 1.0, 1.0, "", 0.0, "a", null,
                            new SendKeyAction("VK_F1"), "F1", null, "f1", null)
                    ])
                ])
            ]));

            var vm = new LayoutEditorViewModel(repo, new ConfigService());
            vm.LoadLayout("fn-lock");
            vm.SelectedKey = vm.Columns[0].Rows[0].Keys[0];

            vm.ActionBuilder.LoadFromAction(new ToggleFunctionLayerAction());

            Assert.NotNull(vm.SelectedKey);
            Assert.False(vm.SelectedKey!.CanEditFunctionOverrides);
            Assert.Null(vm.SelectedKey.FunctionAction);
            Assert.Null(vm.SelectedKey.FunctionLabel);
            Assert.Null(vm.SelectedKey.FunctionEnglishLabel);
        });
    }

    [Fact]
    public void AutoFillFunctionLabels_uses_function_send_key_mapping()
    {
        RunSta(() =>
        {
            var repo = new FakeLayoutRepository();
            repo.Seed("fn-autofill", new LayoutConfig("fn-autofill", null,
            [
                new KeyColumn(0,
                [
                    new KeyRow([
                        new KeySlot("A", null, new SendKeyAction("VK_A"), 1.0, 1.0, "", 0.0, "a", null)
                    ])
                ])
            ]));

            var vm = new LayoutEditorViewModel(repo, new ConfigService());
            vm.LoadLayout("fn-autofill");
            vm.SelectedKey = vm.Columns[0].Rows[0].Keys[0];

            vm.FunctionActionBuilder.LoadFromAction(new SendKeyAction("VK_F1"));
            vm.AutoFillFunctionLabelsCommand.Execute(null);

            Assert.NotNull(vm.SelectedKey);
            Assert.Equal("F1", vm.SelectedKey!.FunctionLabel);
            Assert.Null(vm.SelectedKey.FunctionShiftLabel);
            Assert.Null(vm.SelectedKey.FunctionEnglishLabel);
        });
    }

    private static LayoutConfig CreateTwoColumnLayout(double firstRowHeight = EditableKeySlotVm.DefaultHeightRatio) =>
        new("shared-row", null,
        [
            new KeyColumn(0,
            [
                new KeyRow([CreateKey("1", firstRowHeight)]),
                new KeyRow([CreateKey("Q", EditableKeySlotVm.DefaultHeightRatio)])
            ]),
            new KeyColumn(0,
            [
                new KeyRow([CreateKey("Del", firstRowHeight)])
            ])
        ]);

    private static KeySlot CreateKey(string label, double height) =>
        new(label, null, new SendKeyAction("VK_A"), 1.0, height, "", 0.0, null, null);

    private static void RunSta(Action action)
    {
        Exception? captured = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured is not null)
            throw captured;
    }

    private sealed class FakeLayoutRepository : ILayoutRepository
    {
        private readonly Dictionary<string, LayoutConfig> _layouts = new(StringComparer.OrdinalIgnoreCase);

        public event Action? LayoutsChanged;

        public string DefaultLayoutName => "Basic";

        public LayoutConfig? LastSavedConfig { get; private set; }

        public IReadOnlyList<string> GetAvailableLayouts() => _layouts.Keys.OrderBy(x => x).ToList();

        public LayoutConfig? TryLoad(string name, Action<Exception>? onError = null) =>
            _layouts.TryGetValue(name, out var config)
                ? JsonSerializer.Deserialize<LayoutConfig>(JsonSerializer.Serialize(config, JsonOptions.Default), JsonOptions.Default)
                : null;

        public void Save(string name, LayoutConfig config)
        {
            LastSavedConfig = JsonSerializer.Deserialize<LayoutConfig>(
                JsonSerializer.Serialize(config, JsonOptions.Default),
                JsonOptions.Default)!;
            _layouts[name] = LastSavedConfig;
            LayoutsChanged?.Invoke();
        }

        public bool Delete(string name) => _layouts.Remove(name);

        public void Seed(string name, LayoutConfig config) => _layouts[name] = config;
    }
}
