using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Console = Colorful.Console;

namespace Sample.Parallel.ConsoleApp
{
    class Program
    {
        private static readonly int MaxThreadsToRun = 100;
        private static readonly SemaphoreSlim Semaphore = new SemaphoreSlim(Environment.ProcessorCount);
        private static readonly CountdownEvent Countdown = new CountdownEvent(MaxThreadsToRun);

        private static async Task DoSomethingUsefulAsync(int x)
        {
            var formattedId = $"{x}".PadLeft(3, '0');
            var formattedThreadId = $"{Thread.CurrentThread.ManagedThreadId}".PadLeft(3, '0');

            var r = new Random();
            var sleepTime = r.Next(1000, 5000);
            await Console.Out.WriteLineAsync($"[id:{formattedId}] - [thread:{formattedThreadId}] I did something - in {sleepTime} ms");
            await Task.Delay(sleepTime);
        }

        private static void DoSomethingUseful(int x)
        {
            var formattedId = $"{x}".PadLeft(3, '0');
            var formattedThreadId = $"{Thread.CurrentThread.ManagedThreadId}".PadLeft(3, '0');

            var r = new Random();
            
            var sleepTime = r.Next(1000, 5000);

            Console.WriteLine($"[id:{formattedId}] - [thread:{formattedThreadId}] I did something - in {sleepTime} ms");
            Thread.Sleep(sleepTime);
        }

        private static void TestRxWait() =>
            Enumerable.Range(0, MaxThreadsToRun)
                .ToObservable()
                .SubscribeOn(ThreadPoolScheduler.Instance)
                .Select(x => Observable.Defer(() => Observable.Start(() => DoSomethingUseful(x))))
                .Merge(Environment.ProcessorCount)
                .Wait();

        private static async Task TestRxAsync() =>
            await Enumerable.Range(0, MaxThreadsToRun)
                .ToObservable()
                .SubscribeOn(ThreadPoolScheduler.Instance)
                .Select(x =>  Observable.Defer(() => Observable.Start(() => DoSomethingUseful(x))))
                .Merge(Environment.ProcessorCount);

        private static async Task TestTplAsync()
        {
            var action = new ActionBlock<int>(DoSomethingUsefulAsync, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            });

            for (var i = 0; i < MaxThreadsToRun; i++)
                action.Post(i);

            action.Complete();
            await action.Completion;
        }

        private static void TestSemaphore()
        {
            var r = new Random();

            for (var i = 0; i < MaxThreadsToRun; i++)
            {
                Semaphore.Wait();

                var thread = new Thread(p =>
                {
                    DoSomethingUseful(Convert.ToInt32(p));
                    Semaphore.Release();
                    Countdown.Signal();
                });

                thread.Start(i);
            }

            Countdown.Wait();
        }

        private static IEnumerable<string> SplitString(int quantity) =>
            Enumerable.Range(0, quantity).Select(x => "-");

        private static string SplitStringJoin(IEnumerable<string> splitStrings) =>
            string.Join(string.Empty, splitStrings);

        private static void PrintDelimiter() =>
            Console.WriteLine(SplitStringJoin(SplitString(100)), Color.Chartreuse);

        private static void Main()
        {
            Console.WriteLine($"Main thread id {Thread.CurrentThread.ManagedThreadId}");
            PrintDelimiter();

            ThreadPool.GetAvailableThreads(out int workerThreads, out int completionPortThreads);
            Console.WriteLine($"Total available threads {workerThreads} and available threads for use {completionPortThreads}");
            PrintDelimiter();

            Console.WriteLine("--- RX Async execution ---", Color.Crimson);
            PrintDelimiter();
            TestRxAsync().Wait();
            PrintDelimiter();

            Console.WriteLine("--- RX Sync execution ---", Color.Crimson);
            PrintDelimiter();
            TestRxWait();
            PrintDelimiter();

            Console.WriteLine("--- Semaphore execution ---", Color.Crimson);
            PrintDelimiter();
            TestSemaphore();
            PrintDelimiter();

            Console.WriteLine("--- TPL execution ---", Color.Crimson);
            PrintDelimiter();
            TestTplAsync().Wait();
            PrintDelimiter();

            Console.WriteLine("That's all folks... hit [RETURN] to exit!");
            Console.Read();
        }
    }
}