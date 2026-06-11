export interface Product {
  id: string;
  name: string;
  originalPrice: number;
  expirationDate: string | null;
  convertedPricesJson: string;
  createdAt: string;
  importJobId: string;
}

export interface ProductFilter {
  importJobId?: string;
  nameFilter?: string;
  minPrice?: number;
  maxPrice?: number;
  expirationFrom?: string;
  expirationTo?: string;
  sortBy?: string;
  sortOrder?: string;
  page?: number;
  pageSize?: number;
}

export interface PaginatedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
