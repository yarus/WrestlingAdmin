using System;
using System.Collections.Generic;
using System.Diagnostics;
using FluentResults;
using Wrestling.Entities;
using Wrestling.UI.Utils.Localization;

namespace Wrestling.UI.Material.Tournament.Standing.Details
{
    public class GroupGenerator : IGroupGenerator
    {
        // Lazy-resolve via singleton — GroupGenerator is constructed once at DI
        // wire-up time (before the language has been loaded), but Generate is
        // only called after the user fills the dialog, so the service is
        // always ready by then. Fallback keeps tests / pre-init paths
        // readable.
        private static string T(string key, string fallback)
        {
            var value = LocalizationService.Instance?.T(key);
            return string.IsNullOrEmpty(value) || value == key ? fallback : value;
        }

        public Result<IReadOnlyList<AgeWeightGroup>> Generate(string statement, GlobalSettings settings = null)
        {
            try
            {
                var lines = statement.Split(new[] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries);

                var result = new List<AgeWeightGroup>();

                foreach (var line in lines)
                {
                    var parts = line.Split(':');

                    if (parts.Length != 2)
                    {
                        return Result.Fail(T("GroupGen_Error_Format", "Неправильный формат записи. Ожидаемый формат: <Год рождения от>-<Год рождения до>:<вес1>,<вес2>"));
                    }

                    var ages = parts[0].Split('-');
                    var weights = parts[1].Split(',');
                    var gender = parts.Length > 2 ? parts[2] : null;

                    var minAge = ages[0];
                    var maxAge = ages.Length > 1 ? ages[1] : null;

                    foreach (var weight in weights)
                    {
                        if (!int.TryParse(weight, out var parsedWeight))
                        {
                            return Result.Fail(string.Format(T("GroupGen_Error_BadWeight", "Строка '{0}' не может быть переведа в весовую категорию, убедитесь что это число!"), weight));
                        }

                        if (!int.TryParse(minAge, out var parsedMinAge))
                        {
                            return Result.Fail(string.Format(T("GroupGen_Error_BadBirthYear", "Строка '{0}' не может быть переведа в год рождения, убедитесь что это число!"), minAge));
                        }

                        int? nullableMaxAge = null;

                        if (!string.IsNullOrEmpty(maxAge))
                        {
                            if (!int.TryParse(maxAge, out var parsedMaxAge))
                            {
                                return Result.Fail(string.Format(T("GroupGen_Error_BadBirthYear", "Строка '{0}' не может быть переведа в год рождения, убедитесь что это число!"), maxAge));
                            }

                            if (parsedMaxAge < parsedMinAge)
                            {
                                return Result.Fail(string.Format(T("GroupGen_Error_MaxLessThanMin", "Мксимальный год рождения '{0}' должен быть больше минимального '{1}'!"), parsedMaxAge, parsedMinAge));
                            }

                            nullableMaxAge = parsedMaxAge;
                        }

                        var group = new AgeWeightGroup()
                        {
                            ID = Guid.NewGuid(),
                            IsFemale = !string.IsNullOrEmpty(gender) && gender == "Ж",
                            WeightMax = parsedWeight,
                            BirthYearMin = parsedMinAge,
                            BirthYearMax = nullableMaxAge,
                            MaxActionSecond = settings?.MaxActionSecond ?? 30,
                            MaxRoundSecond = settings?.MaxRoundSecond ?? 90,
                            MaxTimeoutSecond = settings?.MaxTimeoutSecond ?? 30
                        };
                    
                        result.Add(group);
                    }
                }

                return Result.Ok(result as IReadOnlyList<AgeWeightGroup>);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GroupGenerator.Generate failed for input: '{statement}'. {ex}");
                return Result.Fail(new Error(string.Format(T("GroupGen_Error_Generic", "Ошибка генерации весовых категорий: {0}"), ex.Message)));
            }
        }
    }    
}