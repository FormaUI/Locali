# Locali Commercial License Agreement

Version 1.0

Copyright © 2026 Eyal Alon <locali@2site.net>

## 1. Parties and Acceptance

This agreement is between Eyal Alon (the **Licensor**) and the individual or organization identified as the customer in an accepted order (the **Customer**).

An **Order** is a checkout record, quotation, or written order confirmation that identifies the Customer, the covered legal entities, the number of seats, the subscription plan, the price and currency, the subscription start and end dates, and this agreement's version. An Order must be accepted by both parties; acceptance may be recorded electronically, including by email. A person accepting for an organization must have authority to bind it.

The Licensor must provide this agreement before acceptance. Commercial permissions begin on the Order's start date once the Order has been accepted and its initial payment has been received, unless the Order expressly provides different payment terms. Sending money without an accepted Order does not by itself grant a license.

## 2. Software and License Grant

The **Software** is the Locali source code, binaries, and accompanying documentation made available by the Licensor for use under this agreement. Third-party components remain governed by their own licenses.

During the active subscription period, and subject to this agreement and the Order, the Licensor grants the Customer a non-exclusive license to install, copy, run, and privately modify the Software for the covered entities' internal development and business activities. This includes development of commercial applications, work on customer projects, and internal automated builds and CI pipelines.

The Customer may use multiple devices and build agents within the seat rules below. General Locali updates made available under this agreement during the subscription may be used for the remainder of the active subscription; no future release or separately sold product is promised.

The Customer owns its application code, localization files, and other original materials. Checking those materials with Locali does not place them under Locali's license. The Customer may sell or distribute applications it develops with Locali, provided those applications do not contain Locali code or binaries unless separately authorized under section 4.

## 3. Seats and Automated Use

A **seat** covers one named developer currently working for a covered entity who either uses the Software directly or actively contributes code or localization content to a project checked by an automated Locali workflow during that work. Employees and contractors may occupy seats. A developer needs only one seat within the entities covered by the Order, even when using multiple devices or projects.

For a shared CI pipeline, each current developer actively working on the checked project on behalf of a covered entity must have a seat while doing that work. Scanning previously written code does not by itself require seats for its historical authors, former employees, or former contractors. Independent external contributors who are not working for the Customer do not require Customer seats merely because the pipeline checks their submissions; their own direct use of Locali requires independently applicable permission. Build agents and service accounts do not require additional seats. People who only read reports and do not otherwise meet the seat criteria do not require seats.

Seats may be reassigned when responsibilities change or a developer leaves, but may not be rotated merely to cover more active developers than the purchased number. Additional seats must be agreed in an updated Order before additional developers begin work that requires seats under this section.

Contractors may receive copies and access solely to perform covered work for the Customer, subject to these terms. The Customer is responsible for their compliance and must end that access when the covered work ends. This limited contractor access does not permit independent use by the contractor or general redistribution.

Only entities identified in the Order are covered. Corporate affiliation alone does not grant commercial rights to another entity.

## 4. Redistribution and External Access

Ordinary subscription seats do not grant rights to resell, sublicense, publish, or otherwise distribute the Software or modified versions to third parties, whether as source, binaries, standalone releases, or part of another product. They also do not grant rights to operate a hosted Locali service for third parties. Internal use and limited contractor access described above remain permitted.

A hosted Locali service means making Locali's checking functions available to external users through a website, API, or other remote interface, whether free or paid. Using Locali within the Customer's development workflow, including work on client projects and sharing resulting reports, is not by itself operating such a service.

Redistribution or hosted-service rights require a separately priced written addendum accepted by both parties. The addendum must expressly identify the authorized products or services, permitted recipients and distribution forms, applicable fees, duration, and recipients' rights after the addendum ends. Payment for ordinary seats does not grant these additional rights.

The contribution exception in section 4 of the Locali Community License accompanying the Software remains available. It permits the specified contribution forks, patches, and review builds without an additional redistribution fee, but does not expand ordinary commercial use or general redistribution rights.

Nothing in this agreement limits permissions independently granted by the applicable hosting platform's terms, including viewing and forking public repositories within GitHub to the extent its terms permit. Those platform permissions do not themselves grant additional standalone distribution, external-use, or branding rights.

## 5. Ownership and Branding

The Licensor and other applicable rights holders retain ownership of the Software. No ownership of the Software is transferred. The Customer retains its rights in its original modifications, subject to the restrictions on using or distributing the underlying Software. This agreement does not require publishing or submitting private modifications.

If the Customer chooses to submit material for inclusion in the official Locali project, the contribution grant and express acceptance process in section 4 of the accompanying community license apply. A contribution must be submitted or approved by someone authorized to grant the relevant rights. Purchasing seats does not itself grant the Licensor rights in the Customer's private modifications.

The Customer must retain existing copyright and license notices in its copies and identify modifications in copies shared under an authorized exception or addendum.

This agreement does not grant permission to use the Locali name, logo, or other branding as another product's or service's identity, or to imply that a third-party offering is official, sponsored, or endorsed by the Licensor. Accurate attribution and descriptions of the Software's origin are permitted, as are clearly identified contribution copies under section 7 of the accompanying community license.

Additional branding permissions require express written authorization. A redistribution addendum does not grant them unless it expressly says so. Uses independently permitted by applicable law are unaffected.

## 6. Fees and Subscription Period

Unless the accepted Order states otherwise, the available plans are:

| Plan    | Price in USD                     | Subscription period |
| ------- | -------------------------------- | ------------------- |
| Monthly | $8 per seat, billed monthly      | One month           |
| Annual  | $72 per seat, billed annually    | One year            |

The Order states the total amount due, including applicable taxes. Fees cover the stated subscription period and are payable in advance unless the Order specifies otherwise. Annual pricing is equivalent to $6 per seat per month, but the annual fee is payable as a yearly charge.

A subscription does not automatically renew unless the Customer expressly agrees to automatic renewal in the Order. Otherwise, renewal requires a new accepted Order and payment. Any automatic renewal must state the renewal period, charge, and cancellation method before acceptance. The Customer may cancel future renewal by emailing locali@2site.net before the next period begins or by using any cancellation facility provided with the Order.

The Customer retains paid rights until the current period ends after cancellation, unless this agreement is terminated earlier under section 8. Cancellation does not create a refund for the unused portion of a period, except where required by applicable law or expressly agreed in writing.

Changes to published prices do not change an already accepted subscription period. Any proposed price or agreement change for renewal must be provided before renewal and expressly accepted by the Customer; otherwise, renewal will not proceed on the changed terms.

## 7. Expiry and Nonpayment

If a subscription expires without renewal, commercial use rights end. A failed renewal payment does not extend the previous subscription. Where an Order allows payment after the period starts, a failure to pay is handled under section 8.

When commercial rights end, the Customer must stop running the Software and its modified versions, including in CI, and remove active installations and access for users and contractors who no longer have permission to use them. Archival or backup copies may be retained solely for recordkeeping, not for continued use under this agreement.

The Customer may instead use the Software under an independently applicable license, including the community license if it meets that license's eligibility and conditions. Expiry does not restart or create a new 30-day community transition period for an organization that has already lost community eligibility.

The Customer may continue using and distributing its own applications, source code, localization files, and reports that do not contain Locali code or binaries. Any separately granted redistribution rights and existing recipients' rights are governed by the relevant addendum.

## 8. Termination for Breach

If either party materially breaches this agreement, the other party may give written notice describing the breach. If it remains unresolved 14 days after receipt of that notice, the notifying party may terminate the agreement by further written notice. This cure period does not extend a subscription that has expired.

Termination of the Customer's commercial rights has the effects described in section 7. If the Customer terminates for the Licensor's uncured material breach, the Licensor will refund the prepaid fees attributable to the unused period after termination. Termination does not erase fees already owed or rights and liabilities that arose before termination.

The Licensor may not end a paid period merely for convenience without the Customer's agreement.

## 9. Support, Warranty, and Liability

The subscription purchases the permissions described here. Guaranteed support response times, custom development, and service-level commitments are included only if stated in the Order.

To the extent permitted by applicable law, the Software is supplied without warranties, including warranties of fitness for a particular purpose, merchantability, or uninterrupted or error-free operation. The Customer is responsible for reviewing Locali's findings and maintaining appropriate backups of its work.

To the extent permitted by applicable law, the Licensor is not liable for indirect or consequential losses, lost profits, or lost data arising from this agreement. The Licensor's total liability under this agreement is limited to the fees paid by the Customer under it during the 12 months preceding the event giving rise to the claim. These limitations do not exclude liability that cannot lawfully be excluded or limited, including liability for fraud where applicable. Nothing in this agreement removes mandatory statutory rights.

## 10. Agreement Documents and Changes

This version of the agreement and the accepted Order form the commercial agreement. An expressly accepted addendum or Order term may override a specific provision here; otherwise, this agreement controls commercial permissions. COMMERCIAL.md is an explanatory pricing and eligibility overview and does not independently expand those permissions.

The community license's eligibility thresholds do not restrict use authorized under an active commercial Order. References to the community license preserve its contribution and attribution permissions and any independently available community use, and identify the terms for voluntarily submitted contributions; they do not impose community eligibility on paid use.

Later website edits or new license versions do not automatically amend an accepted agreement. Changes require both parties' acceptance. Earlier releases obtained under other licenses retain their applicable terms.

If a provision is unenforceable, the remaining provisions continue to apply to the extent permitted by law. Ownership, branding restrictions, obligations following expiry, accrued payment obligations, and the warranty and liability provisions survive expiry or termination to the extent relevant.

Notices to the Licensor must be sent to locali@2site.net. Notices to the Customer must be sent to the contact recorded in the Order. Each party must keep its notice address current.
