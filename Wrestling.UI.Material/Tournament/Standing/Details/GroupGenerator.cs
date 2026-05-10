using System;
using System.Collections.Generic;
using System.Diagnostics;
using FluentResults;
using Wrestling.Entities;

namespace Wrestling.UI.Material.Tournament.Standing.Details
{
    public class GroupGenerator : IGroupGenerator
    {
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
                        return Result.Fail("Неправильный формат записи. Ожидаемый формат: <Год рождения от>-<Год рождения до>:<вес1>,<вес2>");
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
                            return Result.Fail($"Строка '{weight}' не может быть переведа в весовую категорию, убедитесь что это число!");
                        }

                        if (!int.TryParse(minAge, out var parsedMinAge))
                        {
                            return Result.Fail($"Строка '{minAge}' не может быть переведа в год рождения, убедитесь что это число!");
                        }

                        int? nullableMaxAge = null;
                        
                        if (!string.IsNullOrEmpty(maxAge))
                        {
                            if (!int.TryParse(maxAge, out var parsedMaxAge))
                            {
                                return Result.Fail($"Строка '{maxAge}' не может быть переведа в год рождения, убедитесь что это число!");   
                            }

                            if (parsedMaxAge < parsedMinAge)
                            {
                                return Result.Fail($"Мксимальный год рождения '{parsedMaxAge}' должен быть больше минимального '{parsedMinAge}'!");
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
                return Result.Fail(new Error($"Ошибка генерации весовых категорий: {ex.Message}"));
            }
        }
    }    
}