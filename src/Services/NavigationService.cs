// <copyright file="NavigationService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Microsoft.Extensions.DependencyInjection;

    public class NavigationService : ObservableObject, INavigationService
    {
        private readonly IServiceProvider serviceProvider;
        private ObservableObject currentViewModel;

        public ObservableObject CurrentViewModel
        {
            get => this.currentViewModel;
            private set
            {
                if (this.SetProperty(ref this.currentViewModel, value))
                {
                    this.NavigationChanged?.Invoke(value);
                }
            }
        }

        public event Action<ObservableObject>? NavigationChanged;

        public NavigationService(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
            this.currentViewModel = new DefaultViewModel(); // Use a concrete type
        }

        public void NavigateTo(Type viewModelType)
        {
            var viewModel = this.serviceProvider.GetRequiredService(viewModelType) as ObservableObject;
            if (viewModel == null)
            {
                return;
            }

            this.CurrentViewModel = viewModel;

            // Asynchronously initialize the ViewModel if it has an InitializeAsync method
            var initializeMethod = viewModel.GetType().GetMethod("InitializeAsync");
            if (initializeMethod != null)
            {
                _ = initializeMethod.Invoke(viewModel, null);
            }
        }
    }
}
