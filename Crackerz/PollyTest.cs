using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NFluent;
using Polly;
using Polly.CircuitBreaker;

namespace Crackerz
{
    public static class TimeExtensions
    {
        public static TimeSpan Millis(this int from)
        {
            return TimeSpan.FromMilliseconds(from);
        }
    }
    
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
    public class PollyTest
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

            Check.ThatCode(() =>
                           {
                               policy.Execute(() => service.DoSomethingCrucial(7));
                           })
                 .Throws<DosException>();
        }

        [TestMethod]
        public void TwoDifferentExceptionsHandled()
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
                .Or<DosException>()
                .Retry(2);

            var result = policy.Execute(() => service.DoSomethingCrucial(7));

            Check.That(result)
                 .Equals(8);
        }

        [TestMethod]
        public void ConditionFailed()
        {
            var behaviors = new List<Func<int, int>>
                            {
                                a => { throw new DosException(1); },
                                a => a + 1
                            };


            var service = GetService(behaviors);

            var policy = Policy
                .Handle<DosException>(p => p.Code == 0)
                .Retry(2);

            Check.ThatCode(() =>
                           {
                               policy.Execute(() => service.DoSomethingCrucial(7));
                           })
                 .Throws<DosException>();
        }

        [TestMethod]
        public void ConditionMet()
        {
            var behaviors = new List<Func<int, int>>
                            {
                                a => { throw new DosException(0); },
                                a => a + 1
                            };


            var service = GetService(behaviors);

            var policy = Policy
                .Handle<DosException>(p => p.Code == 0)
                .Retry(2);

            Check.ThatCode(() =>
                           {
                               policy.Execute(() => service.DoSomethingCrucial(7));
                           })
                 .DoesNotThrow();
        }

        [TestMethod]
        public void ContextRetryAction()
        {
            var behaviors = new List<Func<int, int>>
                            {
                                a => { throw new UnoException(); },
                                a => { throw new DosException(1); },
                                a => a + 1
                            };


            var service = GetService(behaviors);

            var policy = Policy
                .Handle<UnoException>()
                .Or<DosException>()
                .Retry(3,
                       (exception,
                        i,
                        ctx) => Check.That(ctx[i.ToString(CultureInfo.InvariantCulture)]).Equals(exception.GetType().Name));

            var context = new Dictionary<string, object>();
            context[1.ToString(CultureInfo.InvariantCulture)] = typeof (UnoException).Name;
            context[2.ToString(CultureInfo.InvariantCulture)] = typeof (DosException).Name;

            Check.ThatCode(() =>
                           {
                               policy.Execute(() => service.DoSomethingCrucial(7),
                                              context);
                           })
                 .DoesNotThrow();
        }

        [TestMethod]
        public void WaitAndRetry()
        {
            var behaviors = new List<Func<int, int>>
                            {
                                a => { throw new DosException(100); },
                                a => { throw new DosException(200); },
                                a => { throw new DosException(300); },
                                a => a + 1
                            };


            var service = GetService(behaviors);

            var policy = Policy
                .Handle<DosException>()
                .WaitAndRetry(new []
                              {
                                  100.Millis(),
                                  200.Millis(),
                                  300.Millis()
                              },
                              (e,
                               timeSpan) =>
                              {
                                  Check.That(e)
                                       .IsInstanceOf<DosException>();

                                  var casted = e as DosException;

                                  if (casted != null)
                                  {
                                      Check.That(casted.Code * 1.1)
                                           .IsGreaterThan(timeSpan.TotalMilliseconds);

                                      Check.That(casted.Code * 0.9)
                                           .IsLessThan(timeSpan.TotalMilliseconds);
                                  }
                              });

            Check.ThatCode(() =>
                           {
                               policy.Execute(() => service.DoSomethingCrucial(7));
                           })
                 .DoesNotThrow();
        }

        [TestMethod]
        public void CircuitBreaker()
        {
            var behaviors = new List<Func<int, int>>
                            {
                                a => { throw new UnoException(); },
                                a => { throw new UnoException(); },
                                a => { throw new UnoException(); },
                                a => a + 1
                            };


            var service = GetService(behaviors);

            var policy = Policy
                .Handle<UnoException>()
                .CircuitBreaker(2,
                                100.Millis());

            Check.ThatCode(() =>
                           {
                               policy.Execute(() => service.DoSomethingCrucial(7));
                           })
                 .Throws<UnoException>();

            Check.ThatCode(() =>
                           {
                               policy.Execute(() => service.DoSomethingCrucial(7));
                           })
                 .Throws<UnoException>();

            Check.ThatCode(() =>
                           {
                               policy.Execute(() => service.DoSomethingCrucial(7));
                           })
                 .Throws<BrokenCircuitException>();                      
            
            Thread.Sleep(150.Millis());

            Check.ThatCode(() =>
                           {
                               policy.Execute(() => service.DoSomethingCrucial(7));
                           })
                 .Throws<UnoException>();

            Thread.Sleep(150.Millis());

            Check.ThatCode(() =>
                           {
                               policy.Execute(() => service.DoSomethingCrucial(7));
                           })
                 .DoesNotThrow();
        }
    }
}
