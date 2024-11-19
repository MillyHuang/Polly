using Polly;
using Polly.Retry;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace PollyTests
{
    /// Validates the RetryPolicy behavior under various conditions,
    /// such as successful execution, retries with exponential backoff, and error propagation. 
    public class RetryPolicyTests
    {
        /// Create a RetryPolicy for testing.
        private IAsyncPolicy CreateRetryPolicy(int retryCount, TimeSpan backoffDuration)
        {
            return Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(retryCount, _ => backoffDuration);
        }

        [Fact]
        public async Task RetryPolicy_ShouldExecuteSuccessfullyWithoutRetry()
        {
            // Arrange
            var retryPolicy = CreateRetryPolicy(3, TimeSpan.FromMilliseconds(100)); //retry up to 3 times with 100ms interval
            var executionCounter = 0;

            Func<Task> successfulAction = async () =>
            {
                executionCounter++;
                await Task.CompletedTask; // Simulate work w/o fail
            };

            // Act
            await retryPolicy.ExecuteAsync(successfulAction);

            // Assert
            Assert.Equal(1, executionCounter); // Only executed once
        }

        [Fact]
        public async Task RetryPolicy_ShouldRetryWithExponentialBackoffOnTransientError()
        {
            // Arrange
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt)));

            var executionCounter = 0;

            Func<Task> failingAction = () =>
            {
                executionCounter++;
                throw new HttpRequestException("Transient error");
            };

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => retryPolicy.ExecuteAsync(failingAction));
            Assert.Equal(4, executionCounter); // 1 initial + 3 retries
        }

        [Fact]
        public async Task RetryPolicy_ShouldStopAfterMaxRetries()
        {
            // Arrange
            var maxRetries = 3;
            var retryPolicy = CreateRetryPolicy(maxRetries, TimeSpan.FromMilliseconds(100));

            var executionCounter = 0;
            Func<Task> failingAction = () =>
            {
                executionCounter++;
                throw new Exception("Transient error");
            };

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => retryPolicy.ExecuteAsync(failingAction));
            Assert.Equal(maxRetries, executionCounter); // Stops after max retries
        }

        [Fact]
        public async Task RetryPolicy_ShouldNotRetryForNonTransientError()
        {
            // Arrange
            var retryPolicy = CreateRetryPolicy(3, TimeSpan.FromMilliseconds(100));
            var executionCounter = 0;

            Func<Task> failingAction = () =>
            {
                executionCounter++;
                throw new InvalidOperationException("Non-transient error");
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => retryPolicy.ExecuteAsync(failingAction));
            Assert.Equal(1, executionCounter); // Only executed once
        }

        [Fact]
        public async Task RetryPolicy_ShouldPropagateExceptionMessage()
        {
            // Arrange
            var retryPolicy = CreateRetryPolicy(3, TimeSpan.FromMilliseconds(100));
            var exceptionMessage = "Service unavailable";

            Func<Task> failingAction = () => throw new HttpRequestException(exceptionMessage);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HttpRequestException>(() => retryPolicy.ExecuteAsync(failingAction));
            Assert.Equal(exceptionMessage, exception.Message);
        }
    }
}
