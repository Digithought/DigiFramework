using Microsoft.VisualStudio.TestTools.UnitTesting;
using Digithought.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Digithought.Framework.Tests
{
    [TestClass]
    public class LinqExtensionTests
    {
        class Container { public int Value; };

        [TestMethod]
        public void MaxValueClassTest()
        {
            var data = new[] {
                new Container { Value = 5 },
                new Container { Value = 3 },
                new Container { Value = 13 },
                new Container { Value = 7 },
            };

            var maxValue = data.ValueOfMax(d => d.Value);

            Assert.AreEqual(13, maxValue.Value);
        }

        struct ContainerStruct { public int Value; };

        [TestMethod]
        public void MaxValueStructTest()
        {
            var data = new[] {
                new ContainerStruct { Value = 5 },
                new ContainerStruct { Value = 3 },
                new ContainerStruct { Value = 13 },
                new ContainerStruct { Value = 7 },
            };

            var maxValue = data.ValueOfMax(d => d.Value);

            Assert.AreEqual(13, maxValue.Value);
        }

        [TestMethod]
        public void MaxValueEmptyStructTest()
        {
            var data = new ContainerStruct[0];
            var maxValue = data.ValueOfMax(d => d.Value);
            Assert.AreEqual(0, maxValue.Value);
        }

        [TestMethod]
        public void MaxValueEmptyClassTest()
        {
            var data = new Container[0];
            var maxValue = data.ValueOfMax(d => d.Value);
            Assert.IsNull(maxValue);
        }
    }
}