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
        /// TransactionIdentifier - This is the Id/Token that needs to be acknowledge/finalized
        /// </summary>
        public string TransactionIdentifier { get; set; }

        /// <summary>
        /// Transaction date in UTC
        /// </summary>
        public DateTime TransactionDateUtc { get; set; }

        /// <summary>
        /// Product Id/Sku
        /// </summary>
        public string ProductId { get; set; }


        /// <summary>
        /// Quantity of the purchases product
        /// </summary>
        public int Quantity { get; set; } = 1;
        
        /// <summary>
        /// Product Ids/Skus
        /// </summary>
        public IList<string> ProductIds { get; set; }

        /// <summary>
        /// Indicates whether the subscription renewed automatically. If true, the sub is active, else false the user has canceled.
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

        public bool IsAcknowledged { get; set; }

        public string ObfuscatedAccountId { get; set; }

        public string ObfuscatedProfileId { get; set;  }

        /// <summary>
        /// Developer payload
        /// </summary>
        public string ApplicationUsername { get; set; }
        public string Payload { get; set; }

        public string OriginalJson { get; set; }
        public string Signature { get; set; }

        public static bool operator ==(InAppBillingPurchase left, InAppBillingPurchase right) =>
			Equals(left, right);

		public static bool operator !=(InAppBillingPurchase left, InAppBillingPurchase right) =>
			!Equals(left, right);

		public override bool Equals(object obj) =>
			(obj is InAppBillingPurchase purchase) && Equals(purchase);

		public bool Equals(InAppBillingPurchase other) =>
			(Id, TransactionDateUtc, IsAcknowledged, ProductId, AutoRenewing, PurchaseToken, State, Payload, ObfuscatedAccountId, ObfuscatedProfileId, Quantity, ProductIds, OriginalJson, Signature) ==
			(other.Id, other.TransactionDateUtc, other.IsAcknowledged, other.ProductId, other.AutoRenewing, other.PurchaseToken, other.State, other.Payload, other.ObfuscatedAccountId, other.ObfuscatedProfileId, other.Quantity, other.ProductIds, other.OriginalJson, other.Signature);

		public override int GetHashCode() =>
			(Id, TransactionDateUtc, IsAcknowledged, ProductId, AutoRenewing, PurchaseToken, State, Payload, ObfuscatedAccountId, ObfuscatedProfileId, Quantity, ProductIds, OriginalJson, Signature).GetHashCode();

		/// <summary>
		/// Prints out product
		/// </summary>
		/// <returns></returns>
		public override string ToString() => 
			$"{nameof(ProductId)}:{ProductId}| {nameof(IsAcknowledged)}:{IsAcknowledged} | {nameof(AutoRenewing)}:{AutoRenewing} | {nameof(State)}:{State} | {nameof(Id)}:{Id} | {nameof(ObfuscatedAccountId)}:{ObfuscatedAccountId}  | {nameof(ObfuscatedProfileId)}:{ObfuscatedProfileId}  | {nameof(Signature)}:{Signature}  | {nameof(OriginalJson)}:{OriginalJson}  | {nameof(Quantity)}:{Quantity}";
        
    }

}
