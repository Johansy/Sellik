using CajaApp.ViewModels;

namespace CajaApp.Views;

public partial class PremiumPage : ContentPage
{
    public PremiumPage(PremiumViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
