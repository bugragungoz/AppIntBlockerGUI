using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppIntBlockerGUI.Services
{
    public interface INavigationService
    {
        ObservableObject CurrentViewModel { get; }
        void NavigateTo(Type viewModelType);
        event Action<ObservableObject> NavigationChanged;
    }
} 