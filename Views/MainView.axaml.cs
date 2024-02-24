using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using MercuryMapper.Keybinding;

namespace MercuryMapper.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();

        KeybindManager = new();
        KeyDownEvent.AddClassHandler<TopLevel>(OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        KeyUpEvent.AddClassHandler<TopLevel>(OnKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private readonly SettingsView settingsView = new();

    public readonly KeybindManager KeybindManager;
    
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox) return;
        
        if (e.Key is Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl)
        {
            e.Handled = true;
            return;
        }
        
        KeybindManager.OnKeyDown(e);
    }

    private static void OnKeyUp(object sender, KeyEventArgs e) => e.Handled = true;

    private void MenuItemNew_OnClick(object? sender, RoutedEventArgs e) { }

    private void MenuItemOpen_OnClick(object? sender, RoutedEventArgs e) { }

    private void MenuItemSave_OnClick(object? sender, RoutedEventArgs e) { }

    private void MenuItemSaveAs_OnClick(object? sender, RoutedEventArgs e) { }
    
    private void MenuItemExportMercury_OnClick(object? sender, RoutedEventArgs e) { }
    
    private void MenuItemExportSaturn_OnClick(object? sender, RoutedEventArgs e) { }

    private void MenuItemSettings_OnClick(object? sender, RoutedEventArgs e)
    {
        ContentDialog dialog = new()
        {
            Title = Assets.Lang.Resources.Menu_Settings,
            Content = settingsView,
            IsPrimaryButtonEnabled = false,
            CloseButtonText = Assets.Lang.Resources.Generic_Close,
        };
        
        Dispatcher.UIThread.Post(async () => await dialog.ShowAsync());
    }

    private void MenuItemExit_OnClick(object? sender, RoutedEventArgs e) { }

    private void MenuItemUndo_OnClick(object? sender, RoutedEventArgs e) { }

    private void MenuItemRedo_OnClick(object? sender, RoutedEventArgs e) { }

    private void MenuItemCut_OnClick(object? sender, RoutedEventArgs e) { }

    private void MenuItemCopy_OnClick(object? sender, RoutedEventArgs e) { }

    private void MenuItemPaste_OnClick(object? sender, RoutedEventArgs e) { }

    private void RadioNoteType_IsCheckedChanged(object? sender, RoutedEventArgs e) { }

    private void RadioBonusType_IsCheckedChanged(object? sender, RoutedEventArgs e) { }

    private void MaskDirection_IsCheckedChanged(object? sender, RoutedEventArgs e) { }

    private void ButtonGimmickBpmChange_OnClick(object? sender, RoutedEventArgs e) { }

    private void ButtonGimmickTimeSig_OnClick(object? sender, RoutedEventArgs e) { }

    private void ButtonGimmickHiSpeed_OnClick(object? sender, RoutedEventArgs e) { }

    private void ButtonGimmickStop_OnClick(object? sender, RoutedEventArgs e) { }

    private void ButtonGimmickReverse_OnClick(object? sender, RoutedEventArgs e) { }

    private void ButtonInsert_OnClick(object? sender, RoutedEventArgs e) { }
}