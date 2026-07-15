import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { forkJoin } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService, MessageService } from 'primeng/api';
import { FeaturedPromoItemService, SlotDirection } from '@core/services/featured-promo-item.service';
import { LookupService } from '@core/services/lookup.service';
import {
  FeaturedPromoItem,
  FeaturedPromoItemRequest,
  PromotionLookup,
  TrainingCenterLookup,
} from '@core/models/featured-promo-item.model';
import { FeaturedPromoItemForm, PromoSeed } from '../featured-promo-item-form/featured-promo-item-form';

const CENTER_KEY = 'featured-promo-item-list-center';
const WEEK_KEY = 'featured-promo-item-list-week';

const WEEKDAY_ZH = ['日', '一', '二', '三', '四', '五', '六'];

/** Which cell (day + slot) is currently open in the inline form. */
interface EditingCell {
  iso: string;
  slot: number;
  item: FeaturedPromoItem | null;
  seed: PromoSeed | null;
}

interface DayColumn {
  iso: string;
  label: string;
}

/**
 * FeaturedPromoItem list (上稿作業). Custom layout: a training-center tab bar, a one-week
 * (Mon–Sun) navigator, and a day × slot grid with inline add/edit, Copy/Paste and slot moves.
 */
@Component({
  selector: 'app-featured-promo-item-list',
  imports: [CommonModule, ButtonModule, InputTextModule, TooltipModule, FeaturedPromoItemForm],
  templateUrl: './featured-promo-item-list.html',
  styleUrl: './featured-promo-item-list.scss',
})
export class FeaturedPromoItemList implements OnInit {
  private readonly service = inject(FeaturedPromoItemService);
  private readonly lookups = inject(LookupService);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly slots = [1, 2, 3];

  protected readonly centers = signal<TrainingCenterLookup[]>([]);
  protected readonly promotions = signal<PromotionLookup[]>([]);
  protected readonly activeCenterPkid = signal<number | null>(this.loadCenter());
  protected readonly weekStart = signal<Date>(this.loadWeek());
  protected readonly items = signal<FeaturedPromoItem[]>([]);
  protected readonly loading = signal(false);
  protected readonly editing = signal<EditingCell | null>(null);
  protected readonly clipboard = signal<PromoSeed | null>(null);

  /** The seven Mon–Sun day columns for the current week. */
  protected readonly days = computed<DayColumn[]>(() => {
    const start = this.weekStart();
    return Array.from({ length: 7 }, (_, i) => {
      const d = new Date(start.getFullYear(), start.getMonth(), start.getDate() + i);
      return { iso: toIso(d), label: `${d.getMonth() + 1}/${d.getDate()} (${WEEKDAY_ZH[d.getDay()]})` };
    });
  });

  /** "3/16 -- 3/22" week-range label for the navigator. */
  protected readonly weekRangeLabel = computed(() => {
    const cols = this.days();
    return `${cols[0].label.split(' ')[0]} -- ${cols[6].label.split(' ')[0]}`;
  });

  /** Fast lookup of an item by `${iso}#${slot}`. */
  private readonly index = computed(() => {
    const map = new Map<string, FeaturedPromoItem>();
    for (const it of this.items()) {
      map.set(`${it.scheduleOn}#${it.slot}`, it);
    }
    return map;
  });

  ngOnInit(): void {
    forkJoin({
      centers: this.lookups.trainingCenters(),
      promotions: this.lookups.promotions(),
    }).subscribe({
      next: ({ centers, promotions }) => {
        this.centers.set(centers);
        this.promotions.set(promotions);
        if (this.activeCenterPkid() == null && centers.length) {
          this.activeCenterPkid.set(centers[0].pkid);
          this.persistCenter();
        }
        this.search();
      },
      error: () => this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法載入訓練中心 / 活動資料' }),
    });
  }

  /** Runs the current week + center query. */
  protected search(): void {
    const center = this.activeCenterPkid();
    if (center == null) {
      this.items.set([]);
      return;
    }
    const cols = this.days();
    this.loading.set(true);
    this.editing.set(null);
    this.service
      .query({ trainingCenterPkid: center, scheduleOnFrom: cols[0].iso, scheduleOnTo: cols[6].iso })
      .subscribe({
        next: (rows) => {
          this.items.set(rows);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法載入上稿資料' });
        },
      });
  }

  protected selectCenter(pkid: number): void {
    if (this.activeCenterPkid() === pkid) {
      return;
    }
    this.activeCenterPkid.set(pkid);
    this.persistCenter();
    this.search();
  }

  protected prevWeek(): void {
    this.shiftWeek(-7);
  }

  protected nextWeek(): void {
    this.shiftWeek(7);
  }

  private shiftWeek(days: number): void {
    const s = this.weekStart();
    this.weekStart.set(new Date(s.getFullYear(), s.getMonth(), s.getDate() + days));
    this.persistWeek();
    this.search();
  }

  /** Returns the item occupying a day + slot, or null. */
  protected itemAt(iso: string, slot: number): FeaturedPromoItem | null {
    return this.index().get(`${iso}#${slot}`) ?? null;
  }

  protected isEditing(iso: string, slot: number): boolean {
    const e = this.editing();
    return !!e && e.iso === iso && e.slot === slot;
  }

  // ----- inline add / edit -----

  protected startEdit(iso: string, slot: number): void {
    this.editing.set({ iso, slot, item: this.itemAt(iso, slot), seed: null });
  }

  protected startPaste(iso: string, slot: number): void {
    const seed = this.clipboard();
    if (!seed) {
      return;
    }
    this.editing.set({ iso, slot, item: null, seed });
  }

  protected cancelEdit(): void {
    this.editing.set(null);
  }

  protected onSaved(request: FeaturedPromoItemRequest): void {
    const call = request.pkid > 0 ? this.service.update(request) : this.service.create(request);
    call.subscribe({
      next: () => {
        this.messages.add({ severity: 'success', summary: '已儲存', detail: `版位 ${request.slot} 已儲存` });
        this.editing.set(null);
        this.search();
      },
      error: () => this.messages.add({ severity: 'error', summary: '儲存失敗', detail: '無法儲存上稿資料' }),
    });
  }

  // ----- copy / paste -----

  protected copy(item: FeaturedPromoItem): void {
    this.clipboard.set({ promoCode: item.promoCode, topic: item.topic, description: item.description });
    this.messages.add({ severity: 'info', summary: '已複製', detail: `已複製「${item.promoCode}」` });
  }

  // ----- slot move (+ down, - up) -----

  protected move(item: FeaturedPromoItem, direction: SlotDirection): void {
    this.service.moveSlot(item.pkid, direction).subscribe({
      next: () => this.search(),
      error: () => this.messages.add({ severity: 'error', summary: '移動失敗', detail: '無法調整版位' }),
    });
  }

  // ----- delete -----

  protected confirmDelete(item: FeaturedPromoItem): void {
    this.confirm.confirm({
      header: '刪除確認',
      message: `確定要刪除版位 <b>${item.slot}</b>「${item.promoCode}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.delete(item),
    });
  }

  private delete(item: FeaturedPromoItem): void {
    this.service.delete(item.pkid).subscribe({
      next: () => {
        this.messages.add({ severity: 'success', summary: '已刪除', detail: `版位 ${item.slot} 已刪除` });
        this.search();
      },
      error: () => this.messages.add({ severity: 'error', summary: '刪除失敗', detail: '無法刪除上稿資料' }),
    });
  }

  // ----- session storage helpers -----

  private loadCenter(): number | null {
    const raw = sessionStorage.getItem(CENTER_KEY);
    return raw ? Number(raw) : null;
  }
  private persistCenter(): void {
    const c = this.activeCenterPkid();
    if (c != null) {
      sessionStorage.setItem(CENTER_KEY, String(c));
    }
  }
  private loadWeek(): Date {
    const raw = sessionStorage.getItem(WEEK_KEY);
    return mondayOf(raw ? new Date(`${raw}T00:00:00`) : new Date());
  }
  private persistWeek(): void {
    sessionStorage.setItem(WEEK_KEY, toIso(this.weekStart()));
  }
}

/** Monday (local) of the week containing the given date. */
function mondayOf(d: Date): Date {
  const date = new Date(d.getFullYear(), d.getMonth(), d.getDate());
  const day = date.getDay(); // 0=Sun .. 6=Sat
  const diff = day === 0 ? -6 : 1 - day;
  date.setDate(date.getDate() + diff);
  return date;
}

/** Converts a local Date to an ISO yyyy-MM-dd string (local parts). */
function toIso(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}
