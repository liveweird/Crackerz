using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NFluent;
using Polly;

namespace Crackerz
{
    public interface IHighlyAvailableService
    {
        int DoSomethingCrucial(int a);
    }

    public class UnoException : Exception { }

    public class DosException : Exception
    {
        public int Code { get; set; }

        public DosException(int code)
        {
            Code = code;
        }
    }

    [TestClass]
    public class PollySyncTest
    {
        IHighlyAvailableService GetService(List<Func<int, int>> behaviors)
        {
            var mock = new Mock<IHighlyAvailableService>();
            var idx = -1;
            mock.Setup(a => a.DoSomethingCrucial(It.IsAny<int>()))
                .Callback((int a) => idx++)
                .Returns((int a) => behaviors[idx].Invoke(a));                

            return mock.Object;
        }

        [TestMethod]
        public void SuccessSucceeds()
        {
            var behaviors = new List<Func<int, int>>
                            {
                                a => a + 1
                            };


            var service = GetService(behaviors);

            Check.That(service.DoSomethingCrucial(7))
                 .Equals(8);
        }

        [TestMethod]
        public void TwoSuccessesSucceed()
        {
            var behaviors = new List<Func<int, int>>
                            {
                                a => a + 1,
                                a => a - 1
                            };


            var service = GetService(behaviors);

            Check.That(service.DoSomethingCrucial(7))
                 .Equals(8);

            Check.That(service.DoSomethingCrucial(7))
                 .Equals(6);
        }

        [TestMethod]
        public void FailFails()
        {
            var behaviors = new List<Func<int, int>>
                            {
                                a => { throw new UnoException(); },
                                a => a + 1
                            };

            var service = GetService(behaviors);

            Check.ThatCode(() =>
                           {
                               service.DoSomethingCrucial(7);
                           })
                 .Throws<UnoException>();
        }

        [TestMethod]
        public void SingleRetryNotNeeded()
        {
            var behaviors = new List<Func<int, int>>
                            {
                                a => a + 1,
                                a => a - 1
                            };


            var service = GetService(behaviors);

            var policy = Policy
                .Handle<UnoException>()
                .Retry();

            var result = policy.Execute(() => service.DoSomethingCrucial(7));

            Check.That(result)
                 .Equals(8);

            result = policy.Execute(() => service.DoSomethingCrucial(7));

            Check.That(result)
                 .Equals(6);
        }

        [TestMethod]
        public void SingleRetryNeeded()
        {
            var behaviors = new List<Func<int, int>>
                            {
                                a => { throw new UnoException(); },
                                a => a + 1
                            };


            var service = GetService(behaviors);

            var policy = Policy
                .Handle<UnoException>()
                .Retry(1);

            var result = policy.Execute(() => service.DoSomethingCrucial(7));

            Check.That(result)
                 .Equals(8);
        }

        [TestMethod]
        public void SingleRetryNotEnough()
        {
            var behaviors = new List<Func<int, int>>
                            {
                                a => { throw new UnoException(); },
                                a => { throw new UnoException(); },
                                a => a + 1
                            };


            var service = GetService(behaviors);

            var policy = Policy
                .Handle<UnoException>()
                .Retry(1);

            Check.ThatCode(() =>
                           {
                               policy.Execute(() => service.DoSomethingCrucial(7));
                           })
                 .Throws<UnoException>();
        }

        [TestMethod]
        public void TwoRetriesNeeded()
        {
            var behaviors = new List<Func<int, int>>
                            {
                                a => { throw new UnoException(); },
                                a => { throw new UnoException(); },
                                a => a + 1
                            };


            var service = GetService(behaviors);

            var policy = Policy
                .Handle<UnoException>()
                .Retry(2);

            var result = policy.Execute(() => service.DoSomethingCrucial(7));

            Check.That(result)
                 .Equals(8);
        }

        [TestMethod]
        public void ExceptionUnhandled()
        {
            var behaviors = new List<Func<int, int>>
                            {
                                a => { throw new UnoException(); },
                                a => { throw new DosException(0); },
                                a => a + 1
                            };


            var service = GetService(behaviors);

            var policy = Policy
                .Handle<UnoException>()
                .Retry(2);

            Check.ThatCode(() => { policy.Execute(() => service.DoSomethingCrucial(7)); })
                 .Throws<DosException>();
        }
    }
}
