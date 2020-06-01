using System;
using System.Collections.Generic;

namespace Plugin.InAppBilling
{
	[Preserve(AllMembers = true)]
	public class InAppBillingPurchaseComparer : IEqualityComparer<InAppBillingPurchase>
	{
		public bool Equals(InAppBillingPurchase x, InAppBillingPurchase y) => x.Equals(y);


		public int GetHashCode(InAppBillingPurchase x) => x.GetHashCode();
	}

	/// <summary>
	/// Purchase from in app billing
	/// </summary>
	[Preserve(AllMembers = true)]
	public class InAppBillingPurchase : IEquatable<InAppBillingPurchase>
    {
        /// <summary>
        /// 
        /// </summary>
        public InAppBillingPurchase()
        {
        }

        /// <summary>
        /// Purchase/Order Id
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Trasaction date in UTC
        /// </summary>
        public DateTime TransactionDateUtc { get; set; }

        /// <summary>
        /// Product Id/Sku
        /// </summary>
        public string ProductId { get; set; }

        /// <summary>
        /// Indicates whether the subscritpion renewes automatically. If true, the sub is active, else false the user has canceled.
        /// </summary>
        public bool AutoRenewing { get; set; }

        /// <summary>
        /// Unique token identifying the purchase for a given item
        /// </summary>
        public string PurchaseToken { get; set; }

        /// <summary>
        /// Gets the current purchase/subscription state
        /// </summary>
        public PurchaseState State { get; set; }

        /// <summary>
        /// Gets the current consumption state
        /// </summary>
        public ConsumptionState ConsumptionState { get; set; }

        /// <summary>
        /// Developer payload
        /// </summary>
        public string Payload { get; set; }

		public static bool operator ==(InAppBillingPurchase left, InAppBillingPurchase right) =>
			Equals(left, right);

		public static bool operator !=(InAppBillingPurchase left, InAppBillingPurchase right) =>
			!Equals(left, right);

		public override bool Equals(object obj) =>
			(obj is InAppBillingPurchase purchase) && Equals(purchase);

		public bool Equals(InAppBillingPurchase other) =>
			(Id, TransactionDateUtc, ProductId, AutoRenewing, PurchaseToken, State, Payload) ==
			(other.Id, other.TransactionDateUtc, other.ProductId, other.AutoRenewing, other.PurchaseToken, other.State, other.Payload);

		public override int GetHashCode() =>
			(Id, TransactionDateUtc, ProductId, AutoRenewing, PurchaseToken, State, Payload).GetHashCode();

		/// <summary>
		/// Prints out product
		/// </summary>
		/// <returns></returns>
		public override string ToString() => 
			$"ProductId:{ProductId} | AutoRenewing:{AutoRenewing} | State:{State} | Id:{Id}";
        
    }

}
