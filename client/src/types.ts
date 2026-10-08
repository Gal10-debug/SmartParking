export interface Space {
  id: number;
  label: string;
}
export interface ParkingLot {
  id: string;
  name: string;
  spaces: Space[];
}
export type VehicleClass = 'car' | 'motorcycle' | 'bus' | 'truck';
export interface BoundingBox {
  x: number;
  y: number;
  width: number;
  height: number;
}
export interface VehicleDetection {
  vehicleId: number;
  className: VehicleClass;
  confidence: number;
  box: BoundingBox;
}
export interface Analysis {
  id: string;
  parkingLotId: string;
  createdAt: string;
  imageName: string;
  analyzer: string;
  mode: 'vehicle-detection' | 'legacy-demo';
  vehicleCount: number | null;
  imageWidth: number;
  imageHeight: number;
  imageUrl: string | null;
  totalSpaces: number | null;
  occupiedSpaces: number | null;
  availableSpaces: number | null;
  occupancyPercentage: number | null;
  detections: VehicleDetection[];
}
export interface History {
  items: Analysis[];
  total: number;
  page: number;
  pageSize: number;
}
