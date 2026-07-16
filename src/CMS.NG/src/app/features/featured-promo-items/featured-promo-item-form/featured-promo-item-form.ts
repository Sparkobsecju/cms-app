import { Component, EventEmitter, Input, OnInit, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageService } from 'primeng/api';
import {
  FeaturedPromoItem,
  FeaturedPromoItemRequest,
  PromotionLookup,
} from '@core/models/featured-promo-item.model';

/** Seed values copied from another row (used by Paste). */
export interface PromoSeed {
  promoCode: string;
  topic: string;
  description: string;
}

/**
 * Inline add/edit form for a single FeaturedPromoItem cell (day + slot).
 * Enter a PromoCode, look it up against the promotions list to resolve Promotion_pkid
 * (and pre-fill Topic/Description), then emit a request for the parent to persist.
 */
@Component({
  selector: 'app-featured-promo-item-form',
  imports: [CommonModule, ReactiveFormsModule, ButtonModule, InputTextModule],
  templateUrl: './featured-promo-item-form.html',
  styleUrl: './featured-promo-item-form.scss',
})
export class FeaturedPromoItemForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly messages = inject(MessageService);

  /** Existing row when editing; null when adding a new one. */
  @Input() item: FeaturedPromoItem | null = null;
  /** Seed values from Copy/Paste (new rows only). */
  @Input() seed: PromoSeed | null = null;
  /** Promotions used to resolve the entered PromoCode. */
  @Input() promotions: PromotionLookup[] = [];
  /** Cell context (the day + slot this form fills). */
  @Input({ required: true }) scheduleOn!: string;
  @Input({ required: true }) trainingCenterPkid!: number;
  @Input({ required: true }) slot!: number;

  @Output() readonly saved = new EventEmitter<FeaturedPromoItemRequest>();
  @Output() readonly cancelled = new EventEmitter<void>();

  /** Resolved Promotion FK; null until a valid PromoCode is looked up. */
  protected readonly promotionPkid = signal<number | null>(null);

  protected readonly form = this.fb.group({
    promoCode: this.fb.nonNullable.control('', Validators.required),
    topic: this.fb.nonNullable.control('', Validators.required),
    description: this.fb.nonNullable.control('', Validators.required),
  });

  ngOnInit(): void {
    if (this.item) {
      this.promotionPkid.set(this.item.promotionPkid);
      this.form.setValue({
        promoCode: this.item.promoCode,
        topic: this.item.topic,
        description: this.item.description,
      });
    } else if (this.seed) {
      this.form.setValue({
        promoCode: this.seed.promoCode,
        topic: this.seed.topic,
        description: this.seed.description,
      });
      // Try to resolve the pasted code straight away.
      this.lookupPromoCode();
    }
  }

  /** Resolves the entered PromoCode to its Promotion pkid and pre-fills Topic/Description. */
  protected lookupPromoCode(): void {
    const code = this.form.controls.promoCode.value.trim();
    if (!code) {
      this.promotionPkid.set(null);
      return;
    }
    const match = this.promotions.find((p) => p.promoCode.toLowerCase() === code.toLowerCase());
    if (!match) {
      this.promotionPkid.set(null);
      this.messages.add({ severity: 'warn', summary: '查無活動', detail: `查無活動代碼「${code}」` });
      return;
    }
    this.promotionPkid.set(match.pkid);
    this.form.patchValue({ topic: match.topic, description: match.description });
  }

  protected submit(): void {
    if (this.promotionPkid() == null) {
      this.form.controls.promoCode.markAsTouched();
      this.messages.add({ severity: 'warn', summary: '尚未帶入活動', detail: '請輸入有效的活動代碼並帶入' });
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    const request: FeaturedPromoItemRequest = {
      pkid: this.item?.pkid ?? 0, // ignored by the API on create (IDENTITY)
      scheduleOn: this.scheduleOn,
      trainingCenterPkid: this.trainingCenterPkid,
      slot: this.slot,
      promotionPkid: this.promotionPkid()!,
      topic: v.topic.trim(),
      description: v.description.trim(),
    };
    this.saved.emit(request);
  }

  protected cancel(): void {
    this.cancelled.emit();
  }
}
