using Xunit;
using ZakYip.WheelDiverterSorter.Core.Sorting.Pipeline;
using ZakYip.WheelDiverterSorter.Execution.Pipeline;

namespace ZakYip.WheelDiverterSorter.Execution.Tests.Pipeline;

/// <summary>
/// 测试 SortingPipeline 的基本功能
/// </summary>
public class SortingPipelineTests
{
    [Fact]
    public async Task ExecuteAsync_WithNoMiddleware_CompletesSuccessfully()
    {
        // Arrange
        var pipeline = new SortingPipeline();
        var context = new SortingPipelineContext
        {
            ParcelId = 123,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        await pipeline.ExecuteAsync(context);

        // Assert
        // No exception should be thrown
    }

    [Fact]
    public async Task ExecuteAsync_WithSingleMiddleware_InvokesMiddleware()
    {
        // Arrange
        var wasInvoked = false;
        var middleware = new TestMiddleware(ctx =>
        {
            wasInvoked = true;
            Assert.Equal(123, ctx.ParcelId);
        });

        var pipeline = new SortingPipeline();
        pipeline.Use(middleware);

        var context = new SortingPipelineContext
        {
            ParcelId = 123,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        await pipeline.ExecuteAsync(context);

        // Assert
        Assert.True(wasInvoked);
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleMiddlewares_InvokesInOrder()
    {
        // Arrange
        var invocationOrder = new List<int>();

        var middleware1 = new TestMiddleware(ctx => invocationOrder.Add(1));
        var middleware2 = new TestMiddleware(ctx => invocationOrder.Add(2));
        var middleware3 = new TestMiddleware(ctx => invocationOrder.Add(3));

        var pipeline = new SortingPipeline();
        pipeline.Use(middleware1)
               .Use(middleware2)
               .Use(middleware3);

        var context = new SortingPipelineContext
        {
            ParcelId = 123,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        await pipeline.ExecuteAsync(context);

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, invocationOrder);
    }

    [Fact]
    public async Task ExecuteAsync_MiddlewareCanModifyContext()
    {
        // Arrange
        var middleware = new TestMiddleware(ctx =>
        {
            ctx.CurrentStage = "TestStage";
            ctx.TargetChuteId = 42;
        });

        var pipeline = new SortingPipeline();
        pipeline.Use(middleware);

        var context = new SortingPipelineContext
        {
            ParcelId = 123,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        await pipeline.ExecuteAsync(context);

        // Assert
        Assert.Equal("TestStage", context.CurrentStage);
        Assert.Equal(42, context.TargetChuteId);
    }

    [Fact]
    public async Task ExecuteAsync_MiddlewareCanShortCircuit()
    {
        // Arrange
        var middleware1Invoked = false;
        var middleware2Invoked = false;
        var middleware3Invoked = false;

        var middleware1 = new TestMiddleware(ctx => middleware1Invoked = true);
        var middleware2 = new ShortCircuitMiddleware(() => middleware2Invoked = true);
        var middleware3 = new TestMiddleware(ctx => middleware3Invoked = true);

        var pipeline = new SortingPipeline();
        pipeline.Use(middleware1)
               .Use(middleware2)
               .Use(middleware3);

        var context = new SortingPipelineContext
        {
            ParcelId = 123,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        await pipeline.ExecuteAsync(context);

        // Assert
        Assert.True(middleware1Invoked);
        Assert.True(middleware2Invoked);
        Assert.False(middleware3Invoked); // Should not be invoked due to short circuit
    }

    [Fact]
    public void MiddlewareCount_ReturnsCorrectCount()
    {
        // Arrange
        var pipeline = new SortingPipeline();
        pipeline.Use(new TestMiddleware())
               .Use(new TestMiddleware())
               .Use(new TestMiddleware());

        // Act & Assert
        Assert.Equal(3, pipeline.MiddlewareCount);
    }

    // Helper classes for testing
    private class TestMiddleware : ISortingPipelineMiddleware
    {
        private readonly Action<SortingPipelineContext>? _action;

        public TestMiddleware(Action<SortingPipelineContext>? action = null)
        {
            _action = action;
        }

        public async ValueTask InvokeAsync(SortingPipelineContext context, Func<SortingPipelineContext, ValueTask> next)
        {
            _action?.Invoke(context);
            await next(context);
        }
    }

    private class ShortCircuitMiddleware : ISortingPipelineMiddleware
    {
        private readonly Action? _action;

        public ShortCircuitMiddleware(Action? action = null)
        {
            _action = action;
        }

        public ValueTask InvokeAsync(SortingPipelineContext context, Func<SortingPipelineContext, ValueTask> next)
        {
            _action?.Invoke();
            // Don't call next - short circuit the pipeline
            return ValueTask.CompletedTask;
        }
    }
}
