/** Response model for a featured promo item / homepage slot (上稿作業). */
export interface FeaturedPromoItem {
  /** Primary key (主代碼). int IDENTITY, database-assigned. */
  pkid: number;
  /** Scheduled listing date (上稿日期), ISO yyyy-MM-dd. */
  scheduleOn: string;
  /** Training center FK (訓練中心). */
  trainingCenterPkid: number;
  /** Slot position within the day (版位): 1, 2 or 3. */
  slot: number;
  /** Promotion FK (活動). */
  promotionPkid: number;
  /** Topic / headline (標題). */
  topic: string;
  /** Description (說明). */
  description: string;
  /** Promotion code (活動代碼), resolved via JOIN on promotionPkid. */
  promoCode: string;
  /** Training center name (訓練中心), resolved via JOIN. */
  trainingCenterName: string;
}

/** Write DTO for creating/updating a featured promo item. */
export interface FeaturedPromoItemRequest {
  /** Ignored on create (IDENTITY); identifies the row on update. */
  pkid: number;
  scheduleOn: string;
  trainingCenterPkid: number;
  slot: number;
  /** Resolved on the client from the entered PromoCode. */
  promotionPkid: number;
  topic: string;
  description: string;
}

/** Search DTO for filtering featured promo items (week range + training center). */
export interface FeaturedPromoItemQuery {
  trainingCenterPkid?: number | null;
  /** The selected week's Monday (inclusive). */
  scheduleOnFrom?: string | null;
  /** The selected week's Sunday (inclusive). */
  scheduleOnTo?: string | null;
  slot?: number | null;
}

/** Slim lookup row for a training center (訓練中心) — drives the list tabs. */
export interface TrainingCenterLookup {
  pkid: number;
  name: string;
}

/** Slim lookup row for a promotion (活動) — resolves an entered PromoCode to its pkid. */
export interface PromotionLookup {
  pkid: number;
  promoCode: string;
  topic: string;
  description: string;
}
