using Microsoft.VisualStudio.TestTools.UnitTesting;
using Digithought.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Digithought.Framework.Tests
{
    [TestClass()]
    public class StateMachineTests
    {
        private enum TestState { A, AA, AB, B, BA };
        private enum TestTrigger { AAtoAB, ABtoBA };

        [TestMethod()]
        public void TransitionEventsTest()
        {
            var bEnteredTriggered = false;
            var baEnteredTriggered = false;
            var aaExitedTriggered = false;
            var aExitedTriggered = false;
            var abExitedTriggered = false;
            var abEnteredTriggered = false;

            StateMachine<TestState, TestTrigger>.StateChangedHandler aaExited =
                (o, t) =>
                {
                    if (bEnteredTriggered || baEnteredTriggered || aaExitedTriggered || aExitedTriggered || abExitedTriggered || abEnteredTriggered)
                        Assert.Fail("Out of order state change.");
                    aaExitedTriggered = true;
                };
            StateMachine<TestState, TestTrigger>.StateChangedHandler abEntered = 
                (o, t) =>
                {
                    if (bEnteredTriggered || baEnteredTriggered || !aaExitedTriggered || aExitedTriggered || abExitedTriggered || abEnteredTriggered)
                        Assert.Fail("Out of order state change.");
                    abEnteredTriggered = true;
                };
            StateMachine<TestState, TestTrigger>.StateChangedHandler abExited =
                (o, t) =>
                {
                    if (bEnteredTriggered || baEnteredTriggered || !aaExitedTriggered || aExitedTriggered || abExitedTriggered || !abEnteredTriggered)
                        Assert.Fail("Out of order state change.");
                    abExitedTriggered = true;
                };
            StateMachine<TestState, TestTrigger>.StateChangedHandler aExited =
                (o, t) =>
                {
                    if (bEnteredTriggered || baEnteredTriggered || !aaExitedTriggered || aExitedTriggered || !abExitedTriggered || !abEnteredTriggered)
                        Assert.Fail("Out of order state change.");
                    aExitedTriggered = true;
                };
            StateMachine<TestState, TestTrigger>.StateChangedHandler bEntered =
                (o, t) =>
                {
                    if (bEnteredTriggered || baEnteredTriggered || !aaExitedTriggered || !aExitedTriggered || !abExitedTriggered || !abEnteredTriggered)
                        Assert.Fail("Out of order state change.");
                    bEnteredTriggered = true;
                };
            StateMachine<TestState, TestTrigger>.StateChangedHandler baEntered =
                (o, t) =>
                {
                    if (!bEnteredTriggered || baEnteredTriggered || !aaExitedTriggered || !aExitedTriggered || !abExitedTriggered || !abEnteredTriggered)
                        Assert.Fail("Out of order state change.");
                    baEnteredTriggered = true;
                };

            var machine = new StateMachine<TestState, TestTrigger>(
                new[]
                {
                    new StateMachine<TestState, TestTrigger>.StateInfo(TestState.A, null, null, null, aExited),
                    new StateMachine<TestState, TestTrigger>.StateInfo(TestState.AA, TestState.A, 
                        new[] {
                            new StateMachine<TestState, TestTrigger>.Transition(TestTrigger.AAtoAB, TestState.AB)
                        },
                        null,
                        aaExited
                    ),
                    new StateMachine<TestState, TestTrigger>.StateInfo(TestState.AB, TestState.A,
                        new[] {
                            new StateMachine<TestState, TestTrigger>.Transition(TestTrigger.ABtoBA, TestState.BA)
                        },
                        abEntered,
                        abExited
                    ),
                    new StateMachine<TestState, TestTrigger>.StateInfo(TestState.B, null, null, bEntered),
                    new StateMachine<TestState, TestTrigger>.StateInfo(TestState.BA, TestState.B, null, baEntered),
                },
                TestState.AA
            );
            machine.Fire(TestTrigger.AAtoAB);
            if (bEnteredTriggered || baEnteredTriggered || !aaExitedTriggered || aExitedTriggered || abExitedTriggered || !abEnteredTriggered)
                Assert.Fail("Incorrect state change.");
            machine.Fire(TestTrigger.ABtoBA);
            if (!bEnteredTriggered || !baEnteredTriggered || !aaExitedTriggered || !aExitedTriggered || !abExitedTriggered || !abEnteredTriggered)
                Assert.Fail("Incorrect state change.");
        }

        [TestMethod()]
        public void StateInTest()
        {
            var machine = new StateMachine<TestState, TestTrigger>(
                new[]
                {
                    new StateMachine<TestState, TestTrigger>.StateInfo(TestState.A, null, null),
                    new StateMachine<TestState, TestTrigger>.StateInfo(TestState.AA, TestState.A,
                        new[] {
                            new StateMachine<TestState, TestTrigger>.Transition(TestTrigger.AAtoAB, TestState.AB)
                        }
                    ),
                    new StateMachine<TestState, TestTrigger>.StateInfo(TestState.AB, TestState.A,
                        new[] {
                            new StateMachine<TestState, TestTrigger>.Transition(TestTrigger.ABtoBA, TestState.BA)
                        }
                    ),
                    new StateMachine<TestState, TestTrigger>.StateInfo(TestState.B, null, null),
                    new StateMachine<TestState, TestTrigger>.StateInfo(TestState.BA, TestState.B, null),
                },
                TestState.AA
            );

            Assert.IsTrue(machine.InState(TestState.A));
            Assert.IsTrue(machine.InState(TestState.AA));
            Assert.IsFalse(machine.InState(TestState.AB));
            Assert.IsFalse(machine.InState(TestState.B));
            Assert.IsFalse(machine.InState(TestState.BA));
        }
    }
}