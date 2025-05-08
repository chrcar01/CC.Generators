# CC.Generators

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