using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NFluent;

namespace Crackerz
{
    public interface IHighlyAvailableService
    {
        int DoSomethingCrucial(int a);
    }

    public class UnoException : Exception { }

    public class DueException : Exception
    {
        public int Code { get; set; }

        public DueException(int code)
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
            var idx = 0;
            mock.Setup(a => a.DoSomethingCrucial(It.IsAny<int>()))
                .Returns((int a) => behaviors[idx].Invoke(a))
                .Callback(() => idx++);

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


    }
}
