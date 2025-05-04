using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using System.Reflection;

namespace CC.Generators.Tests;

[TestFixture]
public class CreatorGeneratorTests
{
    [Test]
    public void Demo()
    {
        var inputCompilation = CreateCompilation(@"
using InnerApi.Core.Models;
using InnerApi.Core.Services;
using InnerApi.Core.Repositories;
namespace InnerApi.Core.Tests
{    
    [TestFixture]
    [CC.Generators.CreatorAttribute(Target=typeof(AccountsService))]
    public partial class AccountsServiceTests
    {   
    }
}

namespace InnerApi.Core.Services
{
    using InnerApi.Core.Models; 
    using InnerApi.Core.Repositories;
    public class AccountsService : IAccountsService
    {
        private readonly IAccountsRepository _accountsRepository;
        public AccountsService(IAccountsRepository accountsRepository)
        {
            _accountsRepository = accountsRepository;
        }
        public async Task<Account> GetAccount(string accountId)
        {
            return await _accountsRepository.GetAccount(accountId);
        }
    }

    public interface IAccountsService
    {
        Account GetAccount(string accountId);
    }
}
    
namespace InnerApi.Core.Repositories
{
    using InnerApi.Core.Models;
    public interface IAccountsRepository
    {
        Task<Account> GetAccount(string accountId);
    }
}

namespace InnerApi.Core.Models
{
    public class Account
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
}
");
        var generator = new CreatorGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation, out var diagnostics);
        
        //Assert.That(outputCompilation.SyntaxTrees.Count(), Is.EqualTo(2));

        GeneratorDriverRunResult runResult = driver.GetRunResult();


        var generatorRunResult = runResult.Results.First();
        foreach (var generatedSourceResult in generatorRunResult.GeneratedSources)
        {
            var fileContent = generatedSourceResult.SourceText.ToString();
            Console.WriteLine(fileContent);
        }
        
    }

    private static Compilation CreateCompilation(string source)
        => CSharpCompilation.Create("compilation",
            [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(Binder).GetTypeInfo().Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));
}

