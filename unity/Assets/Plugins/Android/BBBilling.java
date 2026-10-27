// Google Play Billing for Board Battles, called from C# (Scripts/Billing.cs):
//  - buy(activity, productId, subscription, accountTag): opens Google Play's purchase sheet for a product
//    (coin packs and the season pass are one-time products, the ad-free month a subscription)
//  - restore(activity, accountTag): purchases paid for but not yet credited (the app closed mid-way)
//  - finish(token, consumable): once the server has credited it, consume (one-time) or acknowledge (subscription)
// Results go back as UnitySendMessage("Board Battles", "OnPurchase", "productId|token") and
// "OnBillingError", "code". The server checks every token with Google before crediting anything.
package com.ahwa.extras;

import android.app.Activity;

import com.android.billingclient.api.AcknowledgePurchaseParams;
import com.android.billingclient.api.BillingClient;
import com.android.billingclient.api.BillingClientStateListener;
import com.android.billingclient.api.BillingFlowParams;
import com.android.billingclient.api.BillingResult;
import com.android.billingclient.api.ConsumeParams;
import com.android.billingclient.api.PendingPurchasesParams;
import com.android.billingclient.api.ProductDetails;
import com.android.billingclient.api.Purchase;
import com.android.billingclient.api.PurchasesUpdatedListener;
import com.android.billingclient.api.QueryProductDetailsParams;
import com.android.billingclient.api.QueryPurchasesParams;
import com.unity3d.player.UnityPlayer;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

public class BBBilling {
    static BillingClient client;
    static boolean ready;
    static final List<Runnable> waiting = new ArrayList<>();

    static void send(String method, String value) { try { UnityPlayer.UnitySendMessage("Board Battles", method, value); } catch (Throwable ignored) { } }

    static final PurchasesUpdatedListener listener = (result, purchases) -> {
        int code = result.getResponseCode();
        if (code == BillingClient.BillingResponseCode.OK && purchases != null) {
            for (Purchase p : purchases) report(p);
        } else if (code == BillingClient.BillingResponseCode.USER_CANCELED) send("OnBillingError", "cancelled");
        else if (code == BillingClient.BillingResponseCode.ITEM_ALREADY_OWNED) send("OnBillingError", "owned");
        else send("OnBillingError", "code_" + code);
    };

    static void report(Purchase p) {
        if (p.getPurchaseState() == Purchase.PurchaseState.PURCHASED) {
            for (String id : p.getProducts()) send("OnPurchase", id + "|" + p.getPurchaseToken());
        } else if (p.getPurchaseState() == Purchase.PurchaseState.PENDING) send("OnBillingError", "pending");
    }

    /** Connected to Google Play, then `then` (queued while connecting). */
    static void connect(Activity a, Runnable then) {
        if (client == null) {
            client = BillingClient.newBuilder(a.getApplicationContext()).setListener(listener)
                .enablePendingPurchases(PendingPurchasesParams.newBuilder().enableOneTimeProducts().build()).build();
        }
        if (ready && client.isReady()) { then.run(); return; }
        synchronized (waiting) { waiting.add(then); if (waiting.size() > 1) return; }
        client.startConnection(new BillingClientStateListener() {
            @Override public void onBillingSetupFinished(BillingResult r) {
                List<Runnable> run;
                synchronized (waiting) { run = new ArrayList<>(waiting); waiting.clear(); }
                if (r.getResponseCode() == BillingClient.BillingResponseCode.OK) { ready = true; for (Runnable x : run) x.run(); }
                else { ready = false; send("OnBillingError", "unavailable"); }
            }
            @Override public void onBillingServiceDisconnected() { ready = false; }
        });
    }

    public static void buy(final Activity a, final String productId, final boolean subscription, final String accountTag) {
        connect(a, () -> {
            String type = subscription ? BillingClient.ProductType.SUBS : BillingClient.ProductType.INAPP;
            QueryProductDetailsParams q = QueryProductDetailsParams.newBuilder().setProductList(Collections.singletonList(
                QueryProductDetailsParams.Product.newBuilder().setProductId(productId).setProductType(type).build())).build();
            client.queryProductDetailsAsync(q, (r, list) -> {
                if (r.getResponseCode() != BillingClient.BillingResponseCode.OK || list == null || list.isEmpty()) { send("OnBillingError", "no_product"); return; }
                ProductDetails d = list.get(0);
                BillingFlowParams.ProductDetailsParams.Builder pb = BillingFlowParams.ProductDetailsParams.newBuilder().setProductDetails(d);
                if (subscription) {
                    if (d.getSubscriptionOfferDetails() == null || d.getSubscriptionOfferDetails().isEmpty()) { send("OnBillingError", "no_product"); return; }
                    pb.setOfferToken(d.getSubscriptionOfferDetails().get(0).getOfferToken());
                }
                BillingFlowParams flow = BillingFlowParams.newBuilder()
                    .setProductDetailsParamsList(Collections.singletonList(pb.build()))
                    .setObfuscatedAccountId(accountTag).build();
                a.runOnUiThread(() -> {
                    BillingResult res = client.launchBillingFlow(a, flow);
                    if (res.getResponseCode() != BillingClient.BillingResponseCode.OK) send("OnBillingError", "code_" + res.getResponseCode());
                });
            });
        });
    }

    public static void restore(final Activity a) {
        connect(a, () -> {
            for (String type : new String[] { BillingClient.ProductType.INAPP, BillingClient.ProductType.SUBS }) {
                client.queryPurchasesAsync(QueryPurchasesParams.newBuilder().setProductType(type).build(), (r, list) -> {
                    if (r.getResponseCode() != BillingClient.BillingResponseCode.OK || list == null) return;
                    for (Purchase p : list) {
                        boolean done = type.equals(BillingClient.ProductType.SUBS) && p.isAcknowledged();
                        if (!done) report(p);
                    }
                });
            }
        });
    }

    public static void finish(final Activity a, final String token, final boolean consumable) {
        connect(a, () -> {
            if (consumable) client.consumeAsync(ConsumeParams.newBuilder().setPurchaseToken(token).build(), (r, t) -> { });
            else client.acknowledgePurchase(AcknowledgePurchaseParams.newBuilder().setPurchaseToken(token).build(), r -> { });
        });
    }
}
