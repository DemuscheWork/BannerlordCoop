using GameInterface.Utils;
using System;
using Xunit;

namespace GameInterface.Tests.Utils
{
    public class GameThreadDispatcherTests
    {
        [Fact]
        public void ExecuteGuarded_RunsAction_WhenCampaignLoaded()
        {
            var ran = false;

            var completed = GameThreadDispatcher.ExecuteGuarded("TestWork", () => ran = true, () => true);

            Assert.True(ran);
            Assert.True(completed);
        }

        [Fact]
        public void ExecuteGuarded_SkipsAction_WhenCampaignNotLoaded()
        {
            var ran = false;

            var completed = GameThreadDispatcher.ExecuteGuarded("TestWork", () => ran = true, () => false);

            Assert.False(ran);
            Assert.False(completed);
        }

        [Fact]
        public void ExecuteGuarded_ContainsException_AndReportsFailure()
        {
            // GameLoopRunner.Update invokes queued actions unguarded, so a throw from deferred
            // game work would escape into the game's main tick and crash it; the guard must
            // swallow it and report failure instead.
            var completed = GameThreadDispatcher.ExecuteGuarded(
                "TestWork",
                () => throw new InvalidOperationException("boom"),
                () => true);

            Assert.False(completed);
        }
    }
}
