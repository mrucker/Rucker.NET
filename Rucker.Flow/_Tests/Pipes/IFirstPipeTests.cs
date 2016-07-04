﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Rucker.Extensions;
using NUnit.Framework;

namespace Rucker.Flow.Tests
{
    [TestFixture("ReadPipe")]
    [TestFixture("AsyncPipe")]
    [TestFixture("LambdaPipe")]
    [TestFixture("ThreadPipe(1)")]
    [TestFixture("ThreadPipe(2)")] //I assume if we can do two then we can do any number
    [TestFixture("PollPipe")]
    [SuppressMessage("ReSharper", "HeuristicUnreachableCode")]    
    [SuppressMessage("ReSharper", "ReturnValueOfPureMethodIsNotUsed")]
    public class IFirstPipeTests
    {
        #region Fields
        private readonly Func<Func<IEnumerable<string>>, IFirstPipe<string>> _firstPipeFactory;
        #endregion

        #region Constructors
        public IFirstPipeTests(string pipeType)
        {
            if (pipeType == "ReadPipe")
            {
                _firstPipeFactory = production => new ReadPipe<string>(new ReadFunc(production), 1);
            }

            if (pipeType == "AsyncPipe")
            {
                _firstPipeFactory = production => new LambdaFirstPipe<string>(production).Async();
            }

            if (pipeType == "LambdaPipe")
            {
                _firstPipeFactory = production => new LambdaFirstPipe<string>(production);
            }

            if (pipeType == "ThreadPipe(1)")
            {
                _firstPipeFactory = production => new LambdaFirstPipe<string>(production).Thread(1);
            }

            if (pipeType == "ThreadPipe(2)")
            {
                _firstPipeFactory = production => new LambdaFirstPipe<string>(production).Thread(2);
            }

            if (pipeType == "PollPipe")
            {
                _firstPipeFactory = production => new LambdaFirstPipe<string>(production).Poll(TimeSpan.FromMilliseconds(500));
            }
        }
        #endregion

        [Test]
        public void ValuesProducedTest()
        {
            var pipe = _firstPipeFactory(ManyProduction());

            Assert.AreEqual(PipeStatus.Created, pipe.Status);

            Assert.IsTrue(Production().SequenceEqual(pipe.Produces));

            Assert.AreEqual(PipeStatus.Finished, pipe.Status);
        }

        [Test]
        public void ValuesProducedTwiceTest()
        {            
            var pipe = _firstPipeFactory(ManyProduction());

            if (pipe is ReadPipe<string>)
            {
                throw new IgnoreException("ReadPipe, because it was built to work with an old framework that doesn't allow infinite reads, doesn't work for this test");
            }

            Assert.AreEqual(PipeStatus.Created, pipe.Status);

            Assert.IsTrue(Production().SequenceEqual(pipe.Produces));

            Assert.AreEqual(PipeStatus.Finished, pipe.Status);

            Assert.IsTrue(Production().SequenceEqual(pipe.Produces));

            Assert.AreEqual(PipeStatus.Finished, pipe.Status);
        }

        [Test]
        public void ValuesProducedOnceTest()
        {
            var pipe = _firstPipeFactory(SingleProduction());

            Assert.AreEqual(PipeStatus.Created, pipe.Status);

            Assert.IsTrue(Production().SequenceEqual(pipe.Produces));

            Assert.AreEqual(PipeStatus.Finished, pipe.Status);

            Assert.IsTrue(pipe.Produces.None());

            Assert.AreEqual(PipeStatus.Finished, pipe.Status);
        }

        [Test]
        public void FirstErrorTest()
        {
            var pipe = _firstPipeFactory(FirstError);

            Assert.That(pipe.Produces.ToArray, Throws.Exception.Message.EqualTo("First").Or.InnerException.Message.EqualTo("First"));

            Assert.AreEqual(PipeStatus.Errored, pipe.Status);
        }

        [Test]
        public void LastErrorTest()
        {
            var pipe = _firstPipeFactory(LastError);

            Assert.That(pipe.Produces.ToArray, Throws.Exception.Message.EqualTo("Last").Or.InnerException.Message.EqualTo("Last"));

            Assert.AreEqual(PipeStatus.Errored, pipe.Status);
        }

        [Test]
        public void OnlyErrorTest()
        {
            var pipe = _firstPipeFactory(OnlyError);

            Assert.That(pipe.Produces.ToArray, Throws.Exception.Message.EqualTo("Only").Or.InnerException.Message.EqualTo("Only"));

            Assert.AreEqual(PipeStatus.Errored, pipe.Status);
        }

        [Test]
        public void WorkingStatusTest()
        {
            var pipe = _firstPipeFactory(SingleProduction());

            var prod = pipe.Produces.GetEnumerator();

            Assert.AreEqual(PipeStatus.Created, pipe.Status);

            prod.MoveNext();

            Assert.AreEqual(Production().Skip(0).First(), prod.Current);

            Assert.AreEqual(PipeStatus.Working, pipe.Status);

            prod.MoveNext();

            Assert.AreEqual(Production().Skip(1).First(), prod.Current);

            Assert.AreEqual(PipeStatus.Working, pipe.Status);
        }

        [Test]
        public void StopStatusTest()
        {
            var pipe = _firstPipeFactory(ManyProduction());

            var prod = pipe.Produces.GetEnumerator();

            Assert.AreEqual(PipeStatus.Created, pipe.Status);

            prod.MoveNext();

            Assert.AreEqual(Production().First(), prod.Current);

            Assert.AreEqual(PipeStatus.Working, pipe.Status);

            pipe.Stop();

            while(prod.MoveNext()) { }

            Assert.AreEqual(PipeStatus.Stopped, pipe.Status);
        }

        [Test]
        public void StopExceptionTest()
        {
            var pipe = _firstPipeFactory(ManyProduction());

            pipe.Stop();

            Assert.That(pipe.Produces.ToArray, Throws.Exception.Message.Contain("stopped").Or.InnerException.Message.Contain("stopped"));
        }

        #region Private Methods
        private static IEnumerable<string> Production()
        {
            yield return "A";
            yield return "B";
            yield return "C";
        }

        private static Func<IEnumerable<string>> ManyProduction()
        {
            return Production;
        }       

        private static Func<IEnumerable<string>> SingleProduction()
        {
            var block = new BlockingCollection<string>();

            foreach (var item in Production())
            {
                block.Add(item);
            }

            block.CompleteAdding();

            return () => block.GetConsumingEnumerable();
        }
        
        private static IEnumerable<string> FirstError()
        {
            throw new Exception("First");
            
            #pragma warning disable 162
            yield return "A";
            yield return "B";
            yield return "C";
            #pragma warning restore 162
        }

        private static IEnumerable<string> LastError()
        {
            yield return "A";
            yield return "B";
            throw new Exception("Last");
        }

        private static IEnumerable<string> OnlyError()
        {
            throw new Exception("Only");
        }
        #endregion
    }
}