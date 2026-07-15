import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import {
  FeaturedPromoItem,
  FeaturedPromoItemQuery,
  FeaturedPromoItemRequest,
} from '@core/models/featured-promo-item.model';

/** Direction for a slot move (版位 上/下). */
export type SlotDirection = 'up' | 'down';

/** Data access for the FeaturedPromoItem CRUD + slot-move endpoints. */
@Injectable({ providedIn: 'root' })
export class FeaturedPromoItemService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/featured-promo-items`;

  /** GET all items. */
  list(): Observable<FeaturedPromoItem[]> {
    return this.http.get<FeaturedPromoItem[]>(this.baseUrl);
  }

  /** POST a filtered query (week range + training center). */
  query(filter: FeaturedPromoItemQuery): Observable<FeaturedPromoItem[]> {
    return this.http.post<FeaturedPromoItem[]>(`${this.baseUrl}/query`, filter);
  }

  /** GET a single item by pkid (numeric PK). */
  get(pkid: number): Observable<FeaturedPromoItem> {
    return this.http.get<FeaturedPromoItem>(`${this.baseUrl}/${pkid}`);
  }

  /** POST a new item. */
  create(request: FeaturedPromoItemRequest): Observable<FeaturedPromoItem> {
    return this.http.post<FeaturedPromoItem>(this.baseUrl, request);
  }

  /** PUT an existing item (pkid carried in the body). */
  update(request: FeaturedPromoItemRequest): Observable<FeaturedPromoItem> {
    return this.http.put<FeaturedPromoItem>(this.baseUrl, request);
  }

  /** DELETE an item by pkid. */
  delete(pkid: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${pkid}`);
  }

  /** POST a slot move (+ moves down 1→2, - moves up 2→1); swaps with the neighbour. */
  moveSlot(pkid: number, direction: SlotDirection): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${pkid}/move/${direction}`, {});
  }
}
