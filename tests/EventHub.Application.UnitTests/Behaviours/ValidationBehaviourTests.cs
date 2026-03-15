using EventHub.Application.Behaviours;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Moq;

namespace EventHub.Application.UnitTests.Behaviours;

public class ValidationBehaviourTests
{
    public record TestRequest(string Value) : IRequest<string>;

    [Fact]
    public async Task Handle_WhenNoValidatorsRegistered_CallsNext()
    {
        var behaviour = new ValidationBehaviour<TestRequest, string>([]);
        var nextCalled = false;

        var result = await behaviour.Handle(
            new TestRequest("ok"),
            _ => { nextCalled = true; return Task.FromResult("result"); },
            CancellationToken.None);

        Assert.True(nextCalled);
        Assert.Equal("result", result);
    }

    [Fact]
    public async Task Handle_WhenAllValidatorsPass_CallsNext()
    {
        var mockValidator = new Mock<IValidator<TestRequest>>();
        mockValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var behaviour = new ValidationBehaviour<TestRequest, string>([mockValidator.Object]);
        var nextCalled = false;

        await behaviour.Handle(
            new TestRequest("ok"),
            _ => { nextCalled = true; return Task.FromResult("result"); },
            CancellationToken.None);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Handle_WhenValidatorFails_ThrowsValidationException()
    {
        var failures = new List<ValidationFailure>
        {
            new("Value", "Value is required.")
        };
        var mockValidator = new Mock<IValidator<TestRequest>>();
        mockValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(failures));

        var behaviour = new ValidationBehaviour<TestRequest, string>([mockValidator.Object]);

        await Assert.ThrowsAsync<ValidationException>(() =>
            behaviour.Handle(
                new TestRequest(""),
                _ => Task.FromResult("result"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenValidatorFails_DoesNotCallNext()
    {
        var failures = new List<ValidationFailure>
        {
            new("Value", "Value is required.")
        };
        var mockValidator = new Mock<IValidator<TestRequest>>();
        mockValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(failures));

        var behaviour = new ValidationBehaviour<TestRequest, string>([mockValidator.Object]);
        var nextCalled = false;

        await Assert.ThrowsAsync<ValidationException>(() =>
            behaviour.Handle(
                new TestRequest(""),
                _ => { nextCalled = true; return Task.FromResult("result"); },
                CancellationToken.None));

        Assert.False(nextCalled);
    }

    [Fact]
    public async Task Handle_WhenMultipleValidatorsFail_AggregatesFailures()
    {
        var mockValidator1 = new Mock<IValidator<TestRequest>>();
        mockValidator1
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([new ValidationFailure("Value", "Error from validator 1.")]));

        var mockValidator2 = new Mock<IValidator<TestRequest>>();
        mockValidator2
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([new ValidationFailure("Value", "Error from validator 2.")]));

        var behaviour = new ValidationBehaviour<TestRequest, string>(
            [mockValidator1.Object, mockValidator2.Object]);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            behaviour.Handle(
                new TestRequest(""),
                _ => Task.FromResult("result"),
                CancellationToken.None));

        Assert.Equal(2, ex.Errors.Count());
    }
}
