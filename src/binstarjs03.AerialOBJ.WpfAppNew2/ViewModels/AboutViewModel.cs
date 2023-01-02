﻿using System;

using binstarjs03.AerialOBJ.WpfApp.Components;

using CommunityToolkit.Mvvm.Input;

namespace binstarjs03.AerialOBJ.WpfApp.ViewModels;
public partial class AboutViewModel
{
    public GlobalState GlobalState { get; }

    public AboutViewModel(GlobalState globalState)
	{
        GlobalState = globalState;
    }

    public event Action? CloseRequested;

    [RelayCommand]
    private void OnClose()
    {
        CloseRequested?.Invoke();
    }
}
