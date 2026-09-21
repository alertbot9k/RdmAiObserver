using SamplePlugin;

var result = ScenarioLibrary.ValidateAll();
foreach (var failure in result.Failures)
    Console.Error.WriteLine(failure);
Console.WriteLine($"{result.Passed}/{result.Total} offline regression checks passed.");
return result.Failures.Count == 0 ? 0 : 1;
