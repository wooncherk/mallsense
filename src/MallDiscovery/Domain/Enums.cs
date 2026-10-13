namespace MallDiscovery.Domain;

public enum PlaceKind { Store, Dining, Service, Facility, EventSpace }
public enum NodeKind { Entrance, Landmark, Junction, Lift, Escalator, Stairs }
public enum PromotionStatus { Draft, Published, Archived }
public enum ExtractionStatus { Pending, NeedsReview, Confirmed, Failed }
public enum IntentKind { Explore, Gift, Dining, Activity, FindPlace, Directions }
public enum MessageRole { Shopper, Assistant }
public enum InteractionKind { RecommendationShown, DestinationSelected, DirectionsRequested, RouteRecovered }
public enum StaffRole { MallManager, TenantEditor }
public enum RouteStatus { Active, Arrived, Abandoned }
public enum ContentSource { Mall, Tenant }
