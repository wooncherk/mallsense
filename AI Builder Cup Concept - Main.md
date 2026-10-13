# AI Builder Cup Concept \- Main

# AI Builder Cup Concept

## Intelligent Mall Discovery \& Retail Intelligence Platform

## Core Idea

The opportunity is not simply to build better shopping mall websites\.

Many malls already have websites, directories, apps, promotion pages and digital signage, but the customer experience is still fragmented and mostly static\.

At the same time:

- shoppers struggle to discover what a mall has to offer;

- tenants already produce useful promotional content, but much of it remains trapped inside posters and marketing materials;

- mall operators have limited visibility into what shoppers are actually trying to find or achieve\.

The idea is to create an **AI\-powered discovery and intelligence layer for physical retail**\.

The platform connects three parties:

```Plain Text
SHOPPER
   ↕
TENANT
   ↕
MALL
```

More accurately:

```Plain Text
TENANTS
Provide richer information
        ↓
AI structures and understands it
        ↓
SHOPPERS
Get better discovery and navigation
        ↓
Interactions create intent signals
        ↓
MALL
Gets customer intelligence
        ↓
Better campaigns / tenant strategy
        ↓
TENANTS
Get more relevant exposure
```

The concept can be summarized as:

> **Make physical retail easier to explore, conversational and measurable\.**
> 
> 

---

# The Three Sides of the Platform

The platform serves three groups\.

## Shopper

Help shoppers discover:

- shops

- dining

- services

- promotions

- activities

- facilities

- events

through natural conversation\.

It also helps them navigate the mall using conversational, landmark\-based directions\.

---

## Tenant

Help tenants turn the promotional materials they already create into structured, discoverable information\.

Rather than asking tenants to maintain complicated catalogues or manually complete long forms, GenAI helps them provide useful information through:

- poster understanding

- information extraction

- guided clarification

- tagging

- description generation

- audience identification

The more context tenants provide, the better the platform can recommend them to relevant shoppers\.

---

## Mall

Give mall operators new insight into:

- what shoppers are looking for

- emerging demand

- popular intents

- discovery gaps

- promotion interest

- navigation difficulty

- underserved customer needs

This turns shopper conversations into useful business intelligence\.

---

# Important Product Philosophy

We should **not attempt to turn the mall into an e\-commerce catalogue**\.

Asking every tenant to provide:

- full product catalogues

- live inventory

- individual product pricing

- SKU information

- stock quantities

would create too much operational friction\.

It would also remove part of what makes physical shopping different\.

Physical retail should still involve:

- exploration

- browsing

- discovery

- spontaneity

The AI helps that process rather than replacing it\.

For example:

Customer:

> I need a birthday gift for my 7\-year\-old nephew\.
> 
> 

The assistant might respond:

> Building sets or toys could be worth exploring\. Toys“R”Us on Level 2 would be a good place to start\. If he enjoys books or activities, Popular on Level 3 is another option\. Popular also has a children's promotion running this week, so you may want to check that out\.
> 
> 

The system does not claim:

> LEGO Set X costs RM129 and is currently in stock\.
> 
> 

Instead, it helps the shopper decide **where to explore**\.

---

# Core Shopper Experience

The shopper experience should feel like a helpful mall companion rather than a generic chatbot\.

Examples:

> I need something for my mum, but she isn't really into clothes\.
> 
> Where can I have dinner with my parents somewhere near the cinema?
> 
> I have two kids with me\. Is there anything we can do while my wife shops?
> 
> I need a Malaysian gift to bring overseas\.
> 
> I have about one hour before my movie starts\. What can I do nearby?
> 
> 

The AI interprets the intent and recommends places worth exploring\.

It may consider:

- shopper objective

- recipient

- approximate budget

- preferences

- exclusions

- current location

- tenant information

- active promotions

- current events

- opening hours

- accessibility

- time of day

The recommendation should explain **why** a destination may be relevant\.

Example:

### Popular — Level 3

Worth exploring if you're looking for a gift for a child and are unsure what they like\. Books and activity items give you several directions to explore\.

**Current promotion:** Children's Reading Week\.

The AI should not invent:

- products

- prices

- availability

- stock

- promotion terms

All factual information must come from verified mall or tenant data\.

---

# Structured Shopper Intent

One important technical component is converting natural conversations into structured intent\.

Example:

> Something for my mum\. She's not really into clothes though\.
> 
> 

Gemini could produce:

```JSON
{
  "intent": "gift",
  "recipient": "mother",
  "excluded_categories": ["fashion"],
  "preferences": [],
  "budget": null
}
```

Later:

> Actually, she's quite into cooking\.
> 
> 

The existing intent is updated rather than restarted\.

Example:

```JSON
{
  "intent": "gift",
  "recipient": "mother",
  "excluded_categories": ["fashion"],
  "preferences": ["cooking"],
  "budget": null
}
```

This structured intent powers:

1. better shopper recommendations; and

2. anonymized mall analytics\.

---

# Tenant Promotion Intelligence

Most tenants already prepare promotions in the form of:

- posters

- flyers

- social\-media artwork

- PDFs

- images

- campaign banners

The problem is that most of the useful information inside them is **unstructured**\.

Instead of asking tenants to manually re\-enter everything into a long CMS form, the tenant can simply upload the promotional material they already have\.

Example poster:

> MID\-AUTUMN SPECIAL
> Buy 2 Mooncake Sets
> Enjoy 15% Off
> Valid 1–30 September
> 
> 

GenAI could extract:

```Plain Text
Promotion title:
Mid-Autumn Special

Promotion type:
Seasonal / Discount

Offer:
15% off when purchasing 2 mooncake sets

Validity:
1–30 September

Categories:
Food & Beverage
Gifting
Festive
Mooncake
Mid-Autumn Festival
```

This immediately converts an image into structured information that can be searched, recommended and analysed\.

---

# Guided Tenant Data Entry

Extraction is only the first step\.

The AI can then identify what useful information is still missing\.

Instead of showing a long form, it asks simple questions conversationally\.

For example:

> Is this promotion mainly intended for personal purchases, gifting, or both?
> 
> 

Tenant:

> Both, but a lot of companies buy them as corporate gifts\.
> 
> 

AI records:

```Plain Text
Suitable for:
Personal gifting
Family gifting
Corporate gifting
```

Then:

> Is there any advance ordering required?
> 
> 

Tenant:

> Corporate orders need 3 days\.
> 
> 

Then:

> Is the promotion available throughout the day?
> 
> 

Tenant:

> Yes\.
> 
> 

The AI is effectively guiding the tenant through structured data entry without requiring them to understand the underlying schema\.

---

# Principle Behind Tenant Participation

The tenant proposition is:

> **The more useful context you provide, the better we can help relevant shoppers discover you\.**
> 
> 

The tenant is therefore incentivized to enrich their information\.

They are not filling in data merely because mall management requires it\.

They can see a direct benefit:

**better context → better recommendations → better discovery**

Example:

Without enrichment, the AI may know:

> Shop X sells cakes\.
> 
> 

With enriched data, it may know:

> Shop X currently has a Mid\-Autumn promotion suitable for family gifting and corporate gifting, available until 30 September\.
> 
> 

Now a shopper asking:

> I need something nice to bring to my in\-laws this weekend\.
> 
> 

can naturally discover that promotion\.

---

# Tenant Discovery Readiness

The tenant interface could provide lightweight guidance showing how much useful discovery information has been supplied\.

Example:

```Plain Text
Promotion Discovery Readiness

████████░░ 80%

✓ Promotion details extracted
✓ Validity confirmed
✓ Target audience provided
✓ Suitable occasions provided
○ Add approximate price range
○ Add dietary information
```

This should not feel like a punitive score\.

It should be framed as:

> Add more context to help the assistant recommend this promotion more accurately\.
> 
> 

Possible enrichment fields include:

- target audience

- occasion

- approximate price level

- gifting suitability

- age suitability

- dietary information

- participating outlets

- availability period

- restrictions

- advance\-order requirement

- category

- use case

---

# GenAI on the Tenant Side

The tenant workflow creates several meaningful GenAI capabilities\.

### Visual Understanding

Understand poster artwork and extract information from images\.

### Structured Extraction

Convert free\-form promotional material into structured fields\.

### Missing Information Detection

Identify useful information that has not yet been provided\.

### Conversational Clarification

Ask the tenant relevant follow\-up questions\.

### Classification

Automatically classify:

- categories

- audiences

- occasions

- promotion types

- product types

### Description Generation

Generate concise shopper\-friendly descriptions from promotional material\.

### Search and Discovery Enrichment

Create tags and semantic context that improve future shopper recommendations\.

This means GenAI is helping tenants **create the knowledge base**, not only consuming it\.

---

# Mall Knowledge Base

The shopper AI should be grounded in a structured mall knowledge base\.

For each tenant:

- tenant name

- category

- general description

- products/services generally offered

- floor

- unit

- location

- opening hours

- tags

- active promotions

- enriched tenant information

The mall may also provide:

- events

- facilities

- cinema

- toilets

- surau

- nursing rooms

- playgrounds

- parking entrances

- escalators

- lifts

- entrances

- floor\-plan topology

This creates a continuously improving knowledge layer\.

---

# Conversational Indoor Navigation

Indoor navigation can become another important differentiator\.

We do not need to recreate Google Maps\.

Instead, the mall can be represented internally as a graph\.

Example:

```Plain Text
Shop A ─ Shop B ─ Shop C ─ Junction
                           │
                        Escalator
                           │
                         Shop D ─ Shop E ─ Shop F
```

A deterministic routing algorithm such as **Dijkstra or A\*** determines the valid route\.

Example:

```Plain Text
A → B → C → Escalator → D → E → F
```

Gemini does **not** decide the physical route\.

Instead, Gemini translates that verified route into natural human directions\.

Example:

> You are currently near Shop A\. Walk towards Shop B and continue until you reach Shop C\.
> 
> Turn left there and you will see an escalator\. Take it down one level\.
> 
> When you reach Shop D, turn right and continue past Shop E\. Shop F will be ahead\.
> 
> 

This is closer to how people naturally navigate malls\.

People understand:

> Turn left after Uniqlo\.
> 
> 

better than:

> Travel 47 metres northwest\.
> 
> 

---

# Navigation Can Be Interrupted

Real shoppers do not always follow instructions perfectly\.

The navigation should therefore be conversational and recoverable\.

Example:

AI:

> Continue towards Shop C and turn left at the escalator\.
> 
> 

Customer:

> Somehow I'm now standing in front of Shop G\.
> 
> 

The application updates the current node\.

```Plain Text
Original route:
A → B → C → D → E → F

Current position:
G

New route:
G → H → J → F
```

Gemini continues:

> That's fine\. From Shop G, continue towards Shop H\. Turn right after Shop H and continue until you reach Shop J\. Your destination will be just beyond it\.
> 
> 

This creates **self\-correcting conversational navigation**\.

---

# Shopper\-Assisted Location Detection

One problem with indoor navigation is identifying the shopper's current location\.

Instead of relying entirely on:

- GPS

- BLE beacons

- Wi\-Fi triangulation

- specialized hardware

the user can help identify their surroundings\.

Example:

AI:

> Which shops can you see near you?
> 
> 

User:

> Guardian, Starbucks and Watsons\.
> 
> 

The system compares those landmarks with the mall graph and estimates the most likely position\.

---

# Multimodal Location Detection

A stronger experience allows the user to take a photo\.

The shopper taps:

**I'm not sure where I am**

They photograph their surroundings\.

Gemini Vision identifies visible clues\.

Example:

```Plain Text
Detected:

Guardian
Watsons
Starbucks
Level 2 signage
```

The application compares those landmarks against the mall topology\.

Example:

```Plain Text
Likely location:
Level 2, Junction 14

Confidence:
92%
```

The assistant can confirm:

> I think you're near Guardian on Level 2\. Is Starbucks on your left?
> 
> 

User:

> Yes\.
> 
> 

The route continues\.

This creates a useful form of indoor positioning without requiring complex infrastructure\.

---

# Separation Between GenAI and Deterministic Systems

This separation is important technically\.

## Deterministic systems handle:

- physical mall graph

- route calculation

- floor changes

- valid walking connections

- tenant locations

- verified opening hours

- verified promotions

- mall facilities

## GenAI handles:

- shopper intent

- conversational interaction

- recommendation reasoning

- explaining suggestions

- poster understanding

- tenant data extraction

- tenant clarification

- image understanding

- landmark extraction

- location inference

- human\-readable directions

- interruption handling

- business\-intelligence interpretation

This prevents the AI from becoming the source of truth for information that should remain deterministic\.

For example:

> The AI never decides whether two mall locations are physically connected\. The route is generated from verified mall topology\. Gemini converts the verified route into natural directions and interprets human input when the shopper needs help\.
> 
> 

---

# Business Intelligence — The Mall Side

The shopper assistant generates a source of information malls traditionally have difficulty collecting:

> **customer intent**
> 
> 

Traditional analytics may show:

> 1,842 visitors opened the directory\.
> 
> 

Our system could show:

> 426 shoppers were trying to find gifts\.
> 
> 183 were looking specifically for children's gifts\.
> 
> 96 were trying to find activities for children\.
> 
> 72 wanted quiet places for coffee or work\.
> 
> 

This is substantially richer than pageviews\.

---

# Structured Intent for Analytics

Rather than relying purely on raw conversation logs, the system can extract anonymized structured signals\.

Example:

```JSON
{
  "intent": "gift_shopping",
  "recipient": "child",
  "category_interest": [
    "toys",
    "books"
  ],
  "budget_signal": "low-mid",
  "timestamp": "...",
  "location": "Level 2"
}
```

Thousands of these signals can then be aggregated\.

The system begins to understand:

> Why are people visiting?
> 
> What do they struggle to find?
> 
> What needs are increasing?
> 
> 

---

# GenAI\-Powered Business Intelligence

GenAI can analyse aggregated signals and turn them into useful management insights\.

Traditional dashboard:

```Plain Text
Directory views: 12,481
Promotion views: 3,920
```

AI\-powered insight:

> Family\-related discovery requests increased 31% this weekend\.
> 
> Most occurred between 1 PM and 5 PM\.
> 
> Only three destinations were repeatedly recommended for these requests\.
> 
> There may be an opportunity to promote more family\-oriented activities during weekend afternoons\.
> 
> 

Another example:

> Searches relating to quiet coffee or working environments are increasing during weekday afternoons\.
> 
> 

Another:

> Gift\-related demand is high, but shoppers appear to have difficulty finding suitable children's gifts\.
> 
> 

GenAI is therefore not merely generating summaries\.

It is helping management identify patterns, gaps and possible opportunities\.

---

# Tenant \+ Shopper \+ Mall Intelligence

Adding tenants creates a much more complete data loop\.

Suppose the mall detects:

> Searches for children's activities are increasing\.
> 
> 

Meanwhile tenants have uploaded:

- children's workshop posters

- book promotions

- toy campaigns

- family dining offers

Because GenAI has already converted those posters into structured information, the system can connect **customer demand** with **tenant activity**\.

Example:

```Plain Text
Shopper demand:
Children's activities ↑ 31%

Relevant active campaigns:
- Children's Reading Week — Popular
- Weekend Craft Workshop — Tenant X
- Family Meal Promotion — Restaurant Y
```

The platform may then recommend to mall management:

> Several active tenant campaigns already match the increase in family\-related demand\. Consider featuring these campaigns more prominently this weekend\.
> 
> 

This closes the loop between all three groups\.

---

# Operational Intelligence

Possible questions:

- What are shoppers looking for today?

- What categories are trending this weekend?

- What promotions are relevant to current demand?

- Which tenant promotions are being discovered?

- What questions are shoppers repeatedly asking?

- Where are shoppers becoming lost?

- Which areas of the mall are difficult to navigate?

- Which shopper needs currently have few recommendations?

---

# Strategic Intelligence

Longer\-term questions could include:

- What customer needs appear underserved?

- Which categories attract demand but have few relevant tenants?

- Which types of activities do visitors repeatedly request?

- Are there recurring needs that our tenant mix does not serve?

- Which tenant promotions align best with actual shopper demand?

- What types of campaigns should the mall encourage tenants to create?

Potential applications include:

- marketing

- events

- promotional planning

- tenant support

- leasing

- tenant mix

- space planning

---

# The Three\-Sided Value Loop

The complete system can be represented as:

```Plain Text
TENANT
   │
   │ Posters / promotions / information
   ▼
GENAI CONTENT UNDERSTANDING
   │
   │ Structured knowledge
   ▼
SHOPPER DISCOVERY
   │
   │ Conversations / navigation / intent
   ▼
STRUCTURED CUSTOMER SIGNALS
   │
   ▼
GENAI BUSINESS INTELLIGENCE
   │
   ▼
MALL MANAGEMENT
   │
   │ Better campaigns / tenant support / strategy
   ▼
TENANTS
```

Another way to describe it:

### Tenant

Provides richer context\.

### Shopper

Benefits from richer discovery\.

### Mall

Learns from shopper demand\.

### Tenant

Gets better exposure to relevant shoppers\.

The platform becomes more useful as all three sides contribute\.

---

# Why It Fits the AI Builder Cup Theme

The project fits:

**Retail \& Commerce: Intelligent Customer and Business Experiences**

particularly well because it addresses both halves directly\.

## Intelligent Customer Experience

- conversational discovery

- recommendations

- personalization

- multimodal interaction

- indoor navigation

- route recovery

## Intelligent Tenant Experience

- poster understanding

- automated data extraction

- guided content entry

- promotion enrichment

- reduced administrative workload

## Intelligent Business Experience

- customer\-intent analytics

- demand trends

- promotion intelligence

- opportunity detection

- decision support

---

# Judging — Problem Alignment \& Impact

This should be one of the stronger areas\.

The problem is easy to understand:

> Shopping malls contain hundreds of businesses and experiences, but discovery is still largely driven by static directories, maps and advertisements\.
> 
> 

Meanwhile:

> Tenants already produce large amounts of useful promotional content, but much of that information remains unstructured\.
> 
> 

And:

> Mall operators understand traffic and transactions better than they understand shopper intent\.
> 
> 

Our platform connects all three problems\.

---

# Judging — Innovation

The product should not be presented as:

> AI chatbot for malls\.
> 
> 

A stronger description is:

> **An AI discovery and intelligence layer for physical retail\.**
> 
> 

Important differentiators include:

- assisting physical exploration instead of replacing it with e\-commerce

- conversational mall discovery

- landmark\-based indoor navigation

- multimodal location recovery

- GenAI poster understanding

- AI\-guided tenant data enrichment

- structured shopper\-intent extraction

- GenAI\-powered mall BI

- closing the loop between shopper demand and tenant promotions

---

# Judging — Technical Merit

This area has significant potential but needs deliberate implementation\.

A weak version would be:

```Plain Text
User
 ↓
Gemini
 ↓
Vector search
 ↓
Tenant directory
 ↓
Answer
```

That risks looking like a standard RAG implementation\.

Instead, the project can demonstrate several meaningful GenAI capabilities\.

### Shopper AI

1. Natural\-language intent extraction

2. Structured output

3. Conversational context

4. Grounded recommendations

5. Explanation of recommendations

6. Multimodal understanding

7. Landmark recognition

8. Location inference

9. Route explanation

10. Route recovery

### Tenant AI

11. Poster/image understanding

12. Promotion extraction

13. Structured data generation

14. Missing\-information detection

15. Guided clarification

16. Automatic tagging

17. Shopper\-friendly description generation

### Mall AI

18. Intent aggregation

19. Pattern analysis

20. Business insight generation

21. Demand\-gap identification

22. Promotion\-demand matching

This demonstrates GenAI being used throughout the information lifecycle\.

---

# UX Direction — Shopper

The shopper interface should not look like a blank chatbot\.

Possible starting screen:

## What are you looking for today?

Quick prompts:

- 🎁 I need a gift

- 🍜 Find somewhere to eat

- 👨‍👩‍👧 Something to do with the kids

- 🛍️ Help me find a shop

- 🧭 I need directions

- ✨ I just want to explore

The assistant then transitions naturally into conversation\.

Recommendations should appear as structured cards rather than walls of chat text\.

---

# UX Direction — Tenant

The tenant workflow should also avoid looking like a large CMS form\.

Example:

### Add Promotion

**Upload your promotion poster**

`[ Upload Image ]`

After upload:

> We found the following information:
> 
> 

```Plain Text
Mid-Autumn Special
15% off when buying 2 mooncake sets
Valid until 30 September
```

**Does this look correct?**

Then:

> A few more details will help us recommend this promotion to the right shoppers\.
> 
> Is this suitable for gifting?
> 
> 

`Yes / No`

> Is it suitable for corporate gifting?
> 
> 

`Yes / No`

> Is advance ordering required?
> 
> 

The system progressively enriches the promotion\.

This makes data management conversational\.

---

# UX Direction — Mall

The mall management experience can be comparatively simple\.

Example:

## Shopper Intelligence

```Plain Text
Discovery conversations this week
12,481

Top intentions

Dining                 31%
Gift shopping          19%
Family activities      14%
Fashion                 12%
```

### Emerging Trend

**Family activities \+31%**

AI Insight:

> Demand for children's activities increased significantly this weekend\.
> 
> Most requests occurred between 1 PM and 5 PM\.
> 
> Three tenant promotions currently match this demand and may benefit from increased exposure\.
> 
> 

This visually shows how shopper demand connects to tenant activity\.

---

# Technical Stack

We can remain primarily within our existing \.NET stack\.

Suggested architecture:

```Plain Text
Shopper Web Experience
Tenant Portal
Mall Dashboard

        ↓

ASP.NET Core

        ↓

Google Cloud Run

        ↓

Gemini / Vertex AI

        ↓

Application Database
```

Potential supporting technologies:

- ASP\.NET Core

- PostgreSQL / Cloud SQL

- Firestore where useful

- Google Cloud Storage

- Gemini multimodal models

- embeddings / vector search if needed

- structured JSON outputs

There is no need to adopt Python simply because the project uses AI\.

---

# Navigation Data Model

Possible internal representation:

```Plain Text
Mall
 ├─ Floors
 ├─ Nodes
 │   ├─ Shops
 │   ├─ Junctions
 │   ├─ Escalators
 │   ├─ Lifts
 │   └─ Entrances
 │
 └─ Edges
     ├─ Distance
     ├─ Direction
     └─ Accessibility
```

Example:

```Plain Text
Node 101 — Uniqlo
Node 102 — Starbucks
Node 103 — Junction
Node 104 — Escalator
Node 105 — Guardian
```

Dijkstra or A\* calculates the valid route\.

Gemini translates it into understandable landmark\-based instructions\.

---

# Prototype Scope

For the competition, we should build enough to prove the full loop rather than attempting a complete production system\.

A realistic prototype could contain:

- one fictional or modelled mall

- three floors

- 40–60 tenants

- 10–20 promotions

- several events

- several mall facilities

- navigation graph

- basic floor\-plan visualization

- shopper conversational interface

- route guidance

- photo\-based location recovery

- tenant promotion upload

- GenAI poster extraction

- guided tenant enrichment

- mall management intelligence dashboard

The important thing is to show the three perspectives clearly\.

---

# Recommended Demo Flow

A strong demo could follow one connected story\.

## Step 1 — Tenant

A tenant uploads a promotional poster\.

Gemini extracts:

- promotion title

- dates

- offer

- category

The AI asks:

> Is this suitable as a children's gift?
> 
> 

Tenant:

> Yes, mainly ages 6–12\.
> 
> 

The structured knowledge is saved\.

---

## Step 2 — Shopper

A shopper asks:

> I need something for my 8\-year\-old nephew\. I'm not sure what he's into\.
> 
> 

The AI recommends a few directions to explore\.

One recommendation references the promotion uploaded earlier\.

---

## Step 3 — Navigation

The shopper selects a destination\.

The platform generates landmark\-based directions\.

Halfway through:

> I think I went the wrong way\. I'm standing in front of Guardian\.
> 
> 

The route recalculates\.

Optionally, the shopper uses a photo to confirm location\.

---

## Step 4 — Mall

Switch to the mall dashboard\.

Show that:

- gift\-shopping intent is increasing;

- children's gifting is a noticeable subcategory;

- several tenant promotions currently match that demand\.

Gemini summarizes the opportunity\.

This single story demonstrates the whole ecosystem\.

---

# Features We Should Avoid Overbuilding

For the competition, avoid excessive effort on:

- full loyalty systems

- parking payments

- transaction processing

- tenant billing

- full enterprise CMS

- digital signage software

- BLE positioning

- Wi\-Fi triangulation

- complex indoor\-map rendering

- full product inventory integration

- ERP integration

- POS integration

These may become commercial features later but are unnecessary to prove the competition concept\.

---

# Main Risks

## Risk 1 — It looks like a chatbot

Mitigation:

Demonstrate the full information loop:

```Plain Text
Tenant content
→ AI extraction
→ enriched knowledge
→ shopper discovery
→ navigation
→ intent data
→ BI
```

---

## Risk 2 — AI hallucination

Mitigation:

Verified data remains the source of truth\.

AI can explain and reason over facts but should not invent:

- tenant locations

- routes

- promotions

- dates

- prices

- products

- operating hours

---

## Risk 3 — Too Much Tenant Work

Mitigation:

The tenant begins by uploading content they already have\.

GenAI does the first extraction and asks only relevant follow\-up questions\.

The principle is:

> **Use AI to reduce data\-entry burden, not create another system tenants have to maintain\.**
> 
> 

---

## Risk 4 — Tenant Participation Is Low

The system should still be useful using basic mall\-controlled tenant information\.

Tenant enrichment improves discovery but is not mandatory for baseline functionality\.

---

## Risk 5 — No Real Usage Data

The prototype may use simulated customer behaviour\.

We should avoid making unsupported commercial claims\.

Instead, prototype testing could measure:

- discovery time

- number of interactions

- task completion

- stores discovered

- navigation recovery

- promotion discoverability

---

# Future Commercial Direction

If the idea proves viable, it could eventually become a broader mall digital platform\.

Possible future modules:

- mall website

- tenant directory

- tenant CMS

- tenant promotion portal

- promotions engine

- events engine

- interactive directory

- conversational discovery

- indoor navigation

- kiosks

- digital signage

- analytics

- AI business intelligence

- parking integrations

- loyalty integrations

Long\-term positioning:

> **The digital layer between malls, tenants and shoppers\.**
> 
> 

---

# Scalability Beyond Shopping Malls

The underlying architecture can later support other large physical venues\.

Examples:

- outlet malls

- airports

- convention centres

- theme parks

- large hospitals

- university campuses

- mixed\-use developments

Generic structure:

```Plain Text
Venue
 ├─ Places
 ├─ Categories
 ├─ Promotions
 ├─ Events
 ├─ Facilities
 └─ Locations
```

However, for the competition, the concept should remain focused specifically on shopping malls\.

---

# Core Competition Story

The idea can be explained simply:

> Shopping malls contain hundreds of shops, services and experiences, but discovering what is relevant to you is still surprisingly difficult\.
> 
> Our platform lets shoppers simply describe what they are looking for\.
> 
> AI understands their intent and recommends places worth exploring using verified mall, tenant, event and promotion information\.
> 
> Tenants do not need to maintain complicated product catalogues\. They upload the promotional material they already create, and GenAI converts it into structured information while guiding them to provide additional context where useful\.
> 
> Once a shopper chooses somewhere to go, the system provides conversational landmark\-based indoor navigation\.
> 
> If they become lost, they can describe what they see or take a photo, and AI helps identify their location and continue the journey\.
> 
> Every shopper interaction also creates anonymized structured intent data\.
> 
> GenAI then analyses those signals so mall operators can understand what shoppers are actually looking for, identify emerging demand and connect that demand with relevant tenant activity\.
> 
> 

The resulting value proposition is:

### Shopper

**Help me discover what is worth exploring\.**

### Tenant

**Help the right shoppers discover what I have to offer\.**

### Mall

**Help me understand what shoppers actually want\.**

---

# Working Positioning

### Functional

**AI\-powered mall discovery and retail intelligence platform**

### Conceptual

**An AI discovery layer for physical retail**

### Ecosystem

**Connecting shoppers, tenants and malls through AI\-powered discovery and intelligence**

### Customer\-focused

**Helping people discover more of what a mall has to offer**

### Strategic

**Turning physical retail into a conversational, discoverable and measurable experience**

### Longer\-Term

**The digital intelligence layer between malls, tenants and shoppers**

---



[AI Builder Cup Concept \- Conversation Review Agent](https://xsgbkw6ki1di.sg.larksuite.com/wiki/ZdxMwUb3IihCrnk8xABlZw68gbd)

