using Xunit;

namespace Plugin.InAppBilling.Tests;

public sealed class BillingRegistrationTests : IDisposable
{
    public BillingRegistrationTests()
    {
        CrossInAppBilling.Dispose();
        CrossInAppBilling.RegisterImplementation(() => new TestBilling());
    }

    public void Dispose()
    {
        CrossInAppBilling.Dispose();
    }

    [Fact]
    public async Task Registration_is_lazy_and_creates_only_one_instance_for_concurrent_callers()
    {
        var creations = 0;
        CrossInAppBilling.RegisterImplementation(() =>
        {
            Interlocked.Increment(ref creations);
            return new TestBilling();
        });

        Assert.Equal(0, creations);

        var instances = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => CrossInAppBilling.Current)));

        Assert.Equal(1, creations);
        Assert.All(instances, instance => Assert.Same(instances[0], instance));
        Assert.True(CrossInAppBilling.IsSupported);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Registration_after_initialization_is_rejected_without_replacing_the_instance(bool checkSupportFirst)
    {
        var original = new TestBilling();
        CrossInAppBilling.RegisterImplementation(() => original);
        if (checkSupportFirst)
            Assert.True(CrossInAppBilling.IsSupported);
        else
            Assert.Same(original, CrossInAppBilling.Current);

        Assert.Throws<InvalidOperationException>(() =>
            CrossInAppBilling.RegisterImplementation(() => new TestBilling()));

        Assert.Same(original, CrossInAppBilling.Current);
        Assert.False(original.Disposed);
    }

    [Fact]
    public void Dispose_retains_registration_and_creates_a_fresh_instance()
    {
        var first = (TestBilling)CrossInAppBilling.Current;
        CrossInAppBilling.Dispose();
        CrossInAppBilling.Dispose();

        Assert.True(first.Disposed);
        Assert.Equal(1, first.DisposeCount);

        var next = CrossInAppBilling.Current;
        Assert.IsType<TestBilling>(next);
        Assert.NotSame(first, next);
    }

    [Fact]
    public void Registration_can_be_replaced_after_disposal()
    {
        var original = (TestBilling)CrossInAppBilling.Current;
        CrossInAppBilling.Dispose();
        var replacement = new TestBilling();
        CrossInAppBilling.RegisterImplementation(() => replacement);

        Assert.True(original.Disposed);
        Assert.Same(replacement, CrossInAppBilling.Current);
    }

    [Fact]
    public void Null_factory_is_rejected_without_losing_the_registration()
    {
        Assert.Throws<ArgumentNullException>(() => CrossInAppBilling.RegisterImplementation(null));
        Assert.IsType<TestBilling>(CrossInAppBilling.Current);
    }

    [Fact]
    public void Null_factory_result_is_reported_instead_of_falling_back()
    {
        CrossInAppBilling.RegisterImplementation(() => null);

        Assert.Throws<InvalidOperationException>(() => CrossInAppBilling.Current);
        Assert.Throws<InvalidOperationException>(() => CrossInAppBilling.IsSupported);
    }

    [Fact]
    public void Factory_failure_is_reported_instead_of_starting_the_default_backend()
    {
        var failure = new NotSupportedException("Backend failed");
        CrossInAppBilling.RegisterImplementation(() => throw failure);

        Assert.Same(failure, Assert.Throws<NotSupportedException>(() => CrossInAppBilling.Current));
    }

    sealed class TestBilling : BaseInAppBilling
    {
        public bool Disposed { get; private set; }
        public int DisposeCount { get; private set; }
        public override bool InTestingMode { get; set; }

        public override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposed = true;
                DisposeCount++;
            }
            base.Dispose(disposing);
        }

        public override Task<IEnumerable<InAppBillingProduct>> GetProductInfoAsync(ItemType itemType, string[] productIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public override Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(ItemType itemType, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public override Task<InAppBillingPurchase> PurchaseAsync(string productId, ItemType itemType, string obfuscatedAccountId = null, string obfuscatedProfileId = null, string subOfferToken = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public override Task<InAppBillingPurchase> UpgradePurchasedSubscriptionAsync(string newProductId, string purchaseTokenOfOriginalSubscription, SubscriptionProrationMode prorationMode = SubscriptionProrationMode.ImmediateWithTimeProration, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public override Task<bool> ConsumePurchaseAsync(string productId, string transactionIdentifier, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
