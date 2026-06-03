import { Component, Input, Output, EventEmitter, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatButtonModule } from '@angular/material/button';
import { Product, ProductFilter } from '../../models/product.model';

@Component({
  selector: 'app-products-table',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatIconModule,
    MatInputModule,
    MatFormFieldModule,
    MatSelectModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatProgressSpinnerModule,
    MatChipsModule,
    MatButtonModule
  ],
  templateUrl: './products-table.component.html',
  styleUrls: ['./products-table.component.scss']
})
export class ProductsTableComponent implements OnChanges {
  @Input() products: Product[] = [];
  @Input() totalCount = 0;
  @Input() loading = false;
  @Input() filter: ProductFilter = {};
  @Output() filterChange = new EventEmitter<ProductFilter>();
  @Output() sortChange = new EventEmitter<Sort>();

  displayedColumns: string[] = ['name', 'originalPrice', 'convertedPrices', 'expirationDate', 'createdAt'];
  pageSize = 50;
  currentPage = 1;

  // Manual sort state tracking
  private currentSortColumn: string = '';
  private currentSortDirection: 'asc' | 'desc' = 'asc';

  // Local filter state
  nameFilterValue = '';
  minPriceValue = '';
  maxPriceValue = '';
  expirationFromValue: Date | null = null;
  expirationToValue: Date | null = null;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['filter']) {
      this.currentPage = this.filter.page || 1;
      this.pageSize = this.filter.pageSize || 50;
    }
  }

  onSortChange(sort: Sort): void {
    // Manual sort toggle logic
    if (this.currentSortColumn === sort.active) {
      // Same column clicked, toggle direction
      this.currentSortDirection = this.currentSortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      // New column clicked, set to ascending
      this.currentSortColumn = sort.active;
      this.currentSortDirection = 'asc';
    }

    this.sortChange.emit({
      active: this.currentSortColumn,
      direction: this.currentSortDirection
    });
  }

  onPageChange(event: any): void {
    this.currentPage = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.filterChange.emit({
      ...this.filter,
      page: this.currentPage,
      pageSize: this.pageSize
    });
  }

  onNameFilterChange(value: string): void {
    this.nameFilterValue = value;
  }

  onMinPriceChange(value: string): void {
    this.minPriceValue = value;
  }

  onMaxPriceChange(value: string): void {
    this.maxPriceValue = value;
  }

  onExpirationFromChange(value: Date | null): void {
    this.expirationFromValue = value;
  }

  onExpirationToChange(value: Date | null): void {
    this.expirationToValue = value;
  }

  onSearch(): void {
    this.filterChange.emit({
      ...this.filter,
      nameFilter: this.nameFilterValue || '',
      minPrice: this.minPriceValue ? parseFloat(this.minPriceValue) : undefined,
      maxPrice: this.maxPriceValue ? parseFloat(this.maxPriceValue) : undefined,
      expirationFrom: this.expirationFromValue ? this.expirationFromValue.toISOString().split('T')[0] : undefined,
      expirationTo: this.expirationToValue ? this.expirationToValue.toISOString().split('T')[0] : undefined,
      page: 1
    });
  }

  clearFilters(): void {
    this.nameFilterValue = '';
    this.minPriceValue = '';
    this.maxPriceValue = '';
    this.expirationFromValue = null;
    this.expirationToValue = null;
  }

  get totalPages(): number {
    return Math.ceil(this.totalCount / this.pageSize);
  }

  parseConvertedPrices(jsonString: string): Record<string, number> {
    try {
      return JSON.parse(jsonString);
    } catch {
      return {};
    }
  }

  getCurrencyKeys(jsonString: string): string[] {
    return Object.keys(this.parseConvertedPrices(jsonString));
  }

  formatPrice(price: number): string {
    return price.toFixed(2);
  }

  formatDate(dateString: string | null): string {
    if (!dateString) return '-';
    const date = new Date(dateString);
    const month = date.getMonth() + 1;
    const day = date.getDate();
    const year = date.getFullYear();
    
    // Check if date is 12/31/9999
    if (month === 12 && day === 31 && year === 9999) {
      return '---';
    }
    
    return `${month}/${day}/${year}`;
  }

  isRtl(text: string): boolean {
    // Check if text contains Arabic or Hebrew characters (RTL languages)
    const rtlRegex = /[\u0591-\u07FF\uFB1D-\uFDFD\uFE70-\uFEFC]/;
    return rtlRegex.test(text);
  }

  getTextDirection(text: string): 'rtl' | 'ltr' {
    return this.isRtl(text) ? 'rtl' : 'ltr';
  }
}
