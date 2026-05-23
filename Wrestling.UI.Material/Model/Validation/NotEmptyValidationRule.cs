using System.Globalization;
using System.Windows.Controls;
using Wrestling.UI.Utils.Localization;

namespace Wrestling.UI.Material.Model.Validation
{
    public class NotEmptyValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (!string.IsNullOrWhiteSpace((value ?? "").ToString()))
            {
                return ValidationResult.ValidResult;
            }

            var msg = LocalizationService.Instance?.T("Validation_Required");
            if (string.IsNullOrEmpty(msg) || msg == "Validation_Required") msg = "Обязательное поле";
            return new ValidationResult(false, msg);
        }
    }
}