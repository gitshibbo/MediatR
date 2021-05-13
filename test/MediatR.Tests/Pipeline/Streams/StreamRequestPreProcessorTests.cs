using System.Threading;

namespace MediatR.Tests.Pipeline.Streams
{
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using MediatR.Pipeline.Streams;
    using Shouldly;
    using StructureMap;
    using Xunit;

    public class StreamRequestPreProcessorTests
    {
        public class Sing : IRequest<Song>
        {
            public string Message { get; set; }
        }

        public class Song
        {
            public string Message { get; set; }
        }

        public class StreamSingHandler : IStreamRequestHandler<Sing, Song>
        {
            public async IAsyncEnumerable<Song> Handle(Sing request, [EnumeratorCancellation]CancellationToken cancellationToken)
            {
                yield return await Task.Run(() => new Song { Message = request.Message + " Song" });
                yield return await Task.Run(() => new Song { Message = request.Message + " Sang" });
                yield return await Task.Run(() => new Song { Message = request.Message + " Seng" });
            }
        }

        public class StreamSingPreProcessor : IStreamRequestPreProcessor<Sing>
        {
            static int preCounter = 0;

            public Task Process(Sing request, CancellationToken cancellationToken)
            {
                request.Message = $"Pre {++preCounter} {request.Message}";

                return Task.FromResult(0);
            }
        }

        [Fact]
        public async Task Should_run_preprocessors()
        {
            var container = new Container(cfg =>
            {
                cfg.Scan(scanner =>
                {
                    scanner.AssemblyContainingType(typeof(PublishTests));
                    scanner.IncludeNamespaceContainingType<Sing>();
                    scanner.WithDefaultConventions();
                    scanner.AddAllTypesOf(typeof(IStreamRequestHandler<,>));
                    scanner.AddAllTypesOf(typeof(IStreamRequestPreProcessor<>));
                });
                cfg.For(typeof(IStreamPipelineBehavior<,>)).Add(typeof(StreamRequestPreProcessorBehavior<,>));
                cfg.For<ServiceFactory>().Use<ServiceFactory>(ctx => ctx.GetInstance);
                cfg.For<IMediator>().Use<Mediator>();
            });

            var mediator = container.GetInstance<IMediator>();

            var responses = mediator.CreateStream(new Sing { Message = "Sing" });

            int i = 0;
            await foreach (var response in responses)
            {
                if (i == 0)
                {
                    response.Message.ShouldBe("Pre 1 Sing Song");
                }
                else if ( i == 1)
                {
                    response.Message.ShouldBe("Pre 1 Sing Sang");
                }
                else if ( i == 2 )
                {
                    response.Message.ShouldBe("Pre 1 Sing Seng");
                }

                (++i).ShouldBeLessThanOrEqualTo(3);
            }
        }

    }
}