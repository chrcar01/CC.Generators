# CC.Generators

### CC.Generators currently targets projects using Moq for mocking

## Purpose

When unit testing, we often need to create instances of classes that have multiple dependencies. This can lead to a lot of boilerplate code in our test classes, as we have to manually create mocks or stubs for each dependency.  The `CC.Generators` library helps to reduce this boilerplate code by automatically generating methods that create instances of classes with their dependencies.

## Example

Let's say we have the following class with several dependencies:

```csharp
public class CustomerService : ICustomerService
{
    private readonly IRepository _repository;
    private readonly IEmailService _emailService;
    private readonly ILogger _logger;
    public CustomerService(IRepository repository, IEmailService emailService, ILogger logger)
    {
        _repository = repository;
        _emailService = emailService;
        _logger = logger;
    }
}
```

In our test class, we want to create an instance of `CustomerService` without having to manually create all the dependencies. We can use the `CC.Generators` library to automatically generate a method that creates an instance of our class under test for us just by adding the `Creator` attribute to our test class.

```csharp
[CC.Generators.Creator(Target = typeof(CustomerService))]
[TestFixture]
public partial class CustomerServiceTests
{
    [Test]
    public void VerifyCustomerService()
    {
        var service = CreateCustomerService();
        Assert.That(service, Is.Not.Null);
    }
}

```

That will cause the generation of a private method, `CreateCustomerService`, responsible for creating an instance of `CustomerService` with all its dependencies. The generated method will look like this:

```csharp
private static ICustomerService CreateCustomerService(MockBehavior defaultBehavior = MockBehavior.Loose,
    IRepository repository = null,
    IEmailService emailService = null, 
    ILogger logger = null)
{
    return new CustomerService(
        repository ?? new Mock<IRepository>(defaultBehavior).Object,
        emailService ?? new Mock<IEmailService>(defaultBehavior).Object,
        logger ?? new Mock<ILogger>(defaultBehavior).Object
    );
}
```

The `CreateCustomerService` method will create a new instance of `CustomerService` with all its dependencies. If you want to use a specific mock for any of the dependencies, you can pass it as a named argument to the method. If you don't pass a mock, a new mock will be created for that dependency.  You can also specify the `MockBehavior` for the generated mocks by passing it as an argument to the `CreateCustomerService` method. 

```csharp
[Test]
public void VerifyCustomerServiceWithSpecificMocks()
{
    // Setup the only mock we need to customize for this particular test
    var repositoryMock = new Mock<IRepository>(MockBehavior.Strict);
    reositoryMock
        .Setup(x => x.GetCustomerById(It.IsAny<int>()))
        .Returns(new Customer());

    // Using named arguments to pass the specific mock, the other dependencies will be created as new mocks with default behavior MockBehavior.Loose
    var service = CreateCustomerService(repository: repositoryMock.Object);

    Assert.That(service, Is.Not.Null);
}
```

## Partial Mocks

Useful for testing legacy code that we would rather not refactor.  Take the following class as an example:

```csharp
public class AccountService(IAccountRepository accountRepository) : IAccountService
{
    public bool ValidateBalance(int accountId)
    {
        var account = GetAccount(accountId);
        if (account.Balance <= 0m)
        {
            return false;
        }

        return true;
    }

    private Account GetAccount(int accountId)
    {
        var account = accountRepository.GetAccount(accountId);
        // complex code that modifies the account model
        return account;
    }

}
```

We've added the `ValidateBalance` method and want to write a unit test to verify the behavior.  What we could do is mock the `IAccountRespository`:

```csharp
var mockAccountRepo = new Mock<IAccountRepository>(MockBehavior.Strict);
mockAccountRepo
    .Setup(x => x.GetAccount(123))
    .Returns(new Account{ Balance = -12.65m });

var accountService = new AccountService(mockAccountRepo.Object);

var balanceIsValid = accountService.ValidateBalance(123);

Assert.That(balanceIsValid, Is.False);
```

This test is pretty simple, as long as there is only one dependency to setup, it's very clear what's being tested.  However, let's say we've inherited some legacy code, and this class actually has 20 dependencies, and many of them are used inside that `GetAccount` method, now we have to setup things that make testing that `ValidateBalance` difficult.  

One thing we can do is create a Partial Mock, which is a way of mocking only some of the methods on our concrete class.  Using Moq, we have to change the visibility of `GetAccount` to give Moq the ability to mock the method:

```csharp
public class AccountService(IAccountRepository accountRepository) : IAccountService
{
    public bool ValidateBalance(int accountId)
    {
        var account = GetAccount(accountId);
        if (account.Balance <= 0m)
        {
            return false;
        }

        return true;
    }

    // changed from 'private' to 'protected virtual'
    protected virtual Account GetAccount(int accountId)
    {
        var account = accountRepository.GetAccount(accountId);
        // complex code that modifies the account model
        return account;
    }
}
```
Now let's rewrite our test so that we call the real `ValidateBalance` method, but call the mocked `GetAccount` method on the service:

```csharp
var mockAccountService = new Mock<AccountService>(MockBehavior.Strict, Mock.Of<IAccountRepository>())
{
    CallBase = true // Call the real method if it's not mocked
};
mockAccountService.Protected()
    .Setup<Account>("GetAccount", 123)
    .Returns(new Account { Balance = -1m }); // Mock the GetAccount method

var balanceIsValid = mockAccountService.Object.ValidateBalance(123);

Assert.That(balanceIsValid, Is.False);
```

In this simple example, there's almost no benefit of using the Partial Mock.  But let's say the real class has 20 dependencies, in this case, we would need to pass in the list of dependencies which is where the partial mock generator attribute comes in.

For this example, the original TestFixture looks like this:
```csharp
[TestFixture]
public class AccountServiceTests
{
}
```
CC.Generators can create a method that generates the partial mock for us, just change the TestFixture to use `partial` and add the `PartialCreator` attribute:
```csharp
[PartialCreator(Target = typeof(AccountService))]
[TestFixture]
public partial class AccountServiceTests
{
}
```
Now there is a new method, `CreateAccountServicePartial`, we can use that handles all of the dependencies for us.  So creating the partial mock changes this:
```csharp
var mockAccountService = new Mock<AccountService>(MockBehavior.Strict, Mock.Of<IAccountRepository>())
{
    CallBase = true // Call the real method if it's not mocked
};
```
to this:
```csharp
var mockAccountService = CreateAccountServicePartial(MockBehavior.Strict);
```
And we can add mocked dependencies as needed using named parameters:
```csharp
var mockAccountRepo = new Mock<IAccountRepository>();
var mockAccountService = CreateAccountServicePartial(accountRepository: mockAccountRepo.Object);
```

