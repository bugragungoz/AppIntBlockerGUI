using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace AppIntBlockerGUI.Services
{
    public class DefaultViewModel : ObservableObject { }

    public class NavigationService : ObservableObject, INavigationService
    {
        private readonly IServiceProvider _serviceProvider;
        private ObservableObject _currentViewModel;

        public ObservableObject CurrentViewModel
        {
            get => _currentViewModel;
            private set
            {
                if (SetProperty(ref _currentViewModel, value))
                {
                    NavigationChanged?.Invoke(value);
                }
            }
        }

        public event Action<ObservableObject>? NavigationChanged;

        public NavigationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _currentViewModel = new DefaultViewModel(); // Use a concrete type
        }

        public void NavigateTo(Type viewModelType)
        {
            var viewModel = _serviceProvider.GetRequiredService(viewModelType) as ObservableObject;
            if (viewModel == null) return;

            CurrentViewModel = viewModel;

            // Asynchronously initialize the ViewModel if it has an InitializeAsync method
            var initializeMethod = viewModel.GetType().GetMethod("InitializeAsync");
            if (initializeMethod != null)
            {
                _ = initializeMethod.Invoke(viewModel, null);
            }
        }
    }
} 