export interface Space {
  id: number;
  label: string;
}
export interface ParkingLot {
  id: string;
  name: string;
  spaces: Space[];
}
export interface Occupancy {
  spaceId: number;
  label: string;
  occupied: boolean;
  confidence: number;
}
export interface Analysis {
  id: string;
  parkingLotId: string;
  createdAt: string;
  imageName: string;
  analyzer: string;
  totalSpaces: number;
  occupiedSpaces: number;
  availableSpaces: number;
  occupancyPercentage: number;
  spaces: Occupancy[];
}
export interface History {
  items: Analysis[];
  total: number;
  page: number;
  pageSize: number;
}
