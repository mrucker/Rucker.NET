﻿using System;
using System.Linq;
using NUnit.Framework;

namespace Rucker.Flow.Tests
{

    [TestFixture]
    public class JobTests
    {
        [Test]
        public void FailedJobTest1()
        {            
            var errorReporter = new TestingErrorReporter();

            using (var job = new InitializingFailJob { Tracker = { ErrorReporters = { errorReporter } }})
            {
                var thrownException   = Assert.Throws<Exception>(job.Process);
                var reportedException = errorReporter.ReportedExceptions.Single();

                Assert.AreEqual(thrownException, reportedException);
            }
        }

        [Test]
        public void FailedJobTest2()
        {
            var errorReporter = new TestingErrorReporter();

            using (var job = new ProcessingFailJob() { Tracker = { ErrorReporters = { errorReporter } } })
            {
                var thrownException = Assert.Throws<Exception>(job.Process);
                var reportedException = errorReporter.ReportedExceptions.Single();

                Assert.AreEqual(thrownException, reportedException);
            }
        }
    }
}