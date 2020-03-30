using System;
using NSubstitute;
using Sentry.Integrations;
using Sentry.Internal;
using Xunit;
using System.Linq;
using System.Threading.Tasks;
using Sentry.Extensibility;

namespace Sentry.Tests
{
    public class AppDomainUnhandledExceptionIntegrationTests
    {
        private class Fixture
        {
            public IHub Hub { get; set; } = Substitute.For<IHub, IDisposable>();
            public IAppDomain AppDomain { get; set; } = Substitute.For<IAppDomain>();

            public Fixture() => Hub.IsEnabled.Returns(true);

            public AppDomainUnhandledExceptionIntegration GetSut()
                => new AppDomainUnhandledExceptionIntegration(AppDomain);
        }

        private readonly Fixture _fixture = new Fixture();
        public SentryOptions SentryOptions { get; set; } = new SentryOptions();

        [Fact]
        public void Handle_WithException_CaptureEvent()
        {
            var sut = _fixture.GetSut();
            sut.Register(_fixture.Hub, SentryOptions);

            sut.Handle(this, new UnhandledExceptionEventArgs(new Exception(), true));

            _fixture.Hub.Received(1).CaptureEvent(Arg.Any<SentryEvent>());
        }

        [Fact]
        public void Handle_NoException_NoCaptureEvent()
        {
            var sut = _fixture.GetSut();
            sut.Register(_fixture.Hub, SentryOptions);

            sut.Handle(this, new UnhandledExceptionEventArgs(new object(), true));

            _fixture.Hub.DidNotReceive().CaptureEvent(Arg.Any<SentryEvent>());
        }

        [Fact]
        public void Handle_TerminatingTrue_DisposesHub()
        {
            var sut = _fixture.GetSut();
            sut.Register(_fixture.Hub, SentryOptions);

            sut.Handle(this, new UnhandledExceptionEventArgs(new Exception(), true));

            var disposableHub = _fixture.Hub as IDisposable;
            disposableHub.Received(1).Dispose();
        }

        [Fact]
        public void Handle_TerminatingTrue_IsHandled()
        {
            var sut = _fixture.GetSut();
            sut.Register(_fixture.Hub, SentryOptions);

            var exception = new Exception();
            sut.Handle(this, new UnhandledExceptionEventArgs(exception, true));
            Assert.True(exception.Data?.Contains("Sentry:Handled"));
            Assert.Equal(exception.Data["Sentry:Handled"] as bool?, (bool?)false);
            Assert.True(exception.Data?.Contains("Sentry:Mechanism"));


            var stackTraceFactory = Substitute.For<ISentryStackTraceFactory>();
            var exceptionProcessor = new MainExceptionProcessor(SentryOptions, () => stackTraceFactory);
            var @event = new SentryEvent(exception);

            exceptionProcessor.Process(exception, @event);
            Assert.NotNull(@event.SentryExceptions.ToList().FirstOrDefault(p => p.Mechanism?.Handled == false));

        }

        [Fact]
        public void Handle_TerminatingTrue_NoException_DisposesHub()
        {
            var sut = _fixture.GetSut();
            sut.Register(_fixture.Hub, SentryOptions);

            sut.Handle(this, new UnhandledExceptionEventArgs(null, true));

            var disposableHub = _fixture.Hub as IDisposable;
            disposableHub.Received(1).Dispose();
        }

        [Fact]
        public void Handle_TerminatingFalse_DoesNotDisposesHub()
        {
            var sut = _fixture.GetSut();
            sut.Register(_fixture.Hub, SentryOptions);

            sut.Handle(this, new UnhandledExceptionEventArgs(new Exception(), false));

            var disposableHub = _fixture.Hub as IDisposable;
            disposableHub.DidNotReceive().Dispose();
        }

        [Fact]
        public void Register_UnhandledException_Subscribes()
        {
            var sut = _fixture.GetSut();
            sut.Register(_fixture.Hub, SentryOptions);

            _fixture.AppDomain.Received().UnhandledException += sut.Handle;
        }

        [Fact]
        public void Unregister_UnhandledException_Unsubscribes()
        {
            var sut = _fixture.GetSut();
            sut.Register(_fixture.Hub, SentryOptions);
            sut.Unregister(_fixture.Hub);

            _fixture.AppDomain.Received(1).UnhandledException -= sut.Handle;
        }
    }
}
