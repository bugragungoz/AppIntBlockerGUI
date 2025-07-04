// <copyright file="INavigationService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.Services
{
    using System;
    using CommunityToolkit.Mvvm.ComponentModel;

    public interface INavigationService
    {
        ObservableObject CurrentViewModel { get; }

        void NavigateTo(Type viewModelType);

        event Action<ObservableObject> NavigationChanged;
    }
}
