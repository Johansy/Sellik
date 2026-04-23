using CajaApp.Services;
using System.ComponentModel;

namespace CajaApp.Helpers
{
    [ContentProperty(nameof(Key))]
    [AcceptEmptyServiceProvider]
    public class TranslateExtension : IMarkupExtension<BindingBase>
    {
        public string Key { get; set; } = string.Empty;

        public BindingBase ProvideValue(IServiceProvider serviceProvider)
        {
            var binding = new Binding
            {
                Mode   = BindingMode.OneWay,
                Path   = $"[{Key}]",
                Source = LocalizationService.Instance,
            };
            return binding;
        }

        object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) =>
            ProvideValue(serviceProvider);
    }
}