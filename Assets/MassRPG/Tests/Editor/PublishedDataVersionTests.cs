using MassRPG.Data.Publishing;
using MassRPG.Server.Publishing;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class PublishedDataVersionTests
    {
        [Test]
        public void ServerCanActivateNewVersionAndRollbackWithoutMutatingOldManifest()
        {
            var registry = new PublishedVersionRegistry();
            var v146 = new PublishedDataManifest(new PublishedDataVersion(146), 1000, "Known good");
            v146.SetPackageHash("world", "abc");
            registry.Register(v146);
            var v147 = new PublishedDataManifest(new PublishedDataVersion(147), 2000, "Bad balance patch", v146.Version);
            v147.SetPackageHash("world", "def");
            registry.Register(v147);

            Assert.IsTrue(registry.TryActivate(v147.Version));
            Assert.AreEqual(147, registry.Active.Version.Value);
            Assert.IsTrue(registry.TryRollbackToParent());
            Assert.AreEqual(146, registry.Active.Version.Value);
            Assert.AreEqual("abc", registry.Active.PackageHashes["world"]);
        }

        [Test]
        public void PublishedVersionNumbersAreImmutableAndCannotBeRegisteredTwice()
        {
            var registry = new PublishedVersionRegistry();
            registry.Register(new PublishedDataManifest(new PublishedDataVersion(1), 1000, "First"));

            Assert.Throws<System.InvalidOperationException>(() =>
                registry.Register(new PublishedDataManifest(new PublishedDataVersion(1), 2000, "Overwrite attempt")));
        }
    }
}
