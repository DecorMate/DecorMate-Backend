# DecorMate App - Complete UI/UX Design Specification

## Overview
This document provides a comprehensive design specification for the DecorMate mobile application, including all pages, components, colors, typography, spacing, and interaction patterns extracted from the Figma design.

---

## COLOR PALETTE

### Primary Colors
- **Primary Green**: `#61AA73` - Main brand color, used for primary buttons and accents
- **Primary Green Dark**: `#287331` - Darker variant for gradients
- **Primary Green Light**: `#5FAA74` - Lighter variant for buttons
- **Primary Green Gradient Start**: `rgba(121, 213, 147, 1)` - Gradient start
- **Primary Green Gradient End**: `rgba(63, 111, 76, 1)` - Gradient end

### Secondary Colors
- **Beige/Neutral**: `#E5D5C8` - Used for dividers and secondary elements
- **Sage Green**: `#9EA484` - Used for secondary text and links
- **Sage Green Dark**: `#676B55` - Darker sage variant

### Background Colors
- **Background Primary**: `#EDEDED` - Main page background
- **Background White**: `#FFFFFF` - Card backgrounds, input fields
- **Background Light Green**: `#E4FAEB` - Light green backgrounds
- **Background Gradient Header**: Linear gradient from `rgba(171, 196, 170, 1)` to `rgba(237, 237, 237, 1)`

### Text Colors
- **Text Primary/Black**: `#000000` - Main headings and primary text
- **Text Secondary**: `#6D6D6D` - Secondary text, descriptions
- **Text Tertiary**: `#A3A0A0` - Placeholder text
- **Text Dark Brown**: `#675D50` - Brand text color (logo, headings)
- **Text Dark Gray**: `#242424` - Form labels
- **Text Brown**: `#6B3D0C` - Accent text
- **Text Dark Brown 2**: `#5B3308` - Darker brown variant
- **Text Dark Gray 2**: `#2E2E2E` - Dark gray text
- **Text Medium Gray**: `#ABA9A9` - Medium gray text

### Accent Colors
- **Light Purple**: `#E9E7FB` - Light purple accent
- **Purple**: `#8C80D2` - Purple accent
- **Light Pink**: `#FDEBEB` - Light pink accent
- **Pink**: `#E68496` - Pink accent
- **Light Orange**: `#FEEFE2` - Light orange accent
- **Orange**: `#F5BB89` - Orange accent
- **Light Blue**: `#E4F3FA` - Light blue accent
- **Blue**: `#68A3CE` - Blue accent

### Border Colors
- **Border Light**: `#9EA484` - Light border color
- **Border Beige**: `#E5D5C8` - Beige border
- **Border Gray**: `#D2DCF6` - Light gray border

### Social Media Colors
- **Google Blue**: `#4285F4`
- **Google Green**: `#34A853`
- **Google Yellow**: `#FBBC05`
- **Google Red**: `#EA4335`

---

## TYPOGRAPHY

### Font Families
1. **Agbalumo** - Used for brand name/logo and major headings
2. **Poppins** - Primary body font, used for most text
3. **Inika** - Used for specific headings/subheadings

### Typography Scale

#### Headings
- **H1 - Brand/Logo**: 
  - Font: Agbalumo
  - Weight: 400
  - Size: 24px / 28px / 34px (varies by context)
  - Line Height: 1.33em / 1.48em / 0.94em

- **H2 - Page Title**:
  - Font: Agbalumo
  - Weight: 400
  - Size: 30px
  - Line Height: 0.93em

- **H3 - Section Title**:
  - Font: Agbalumo
  - Weight: 400
  - Size: 26px / 24px
  - Line Height: 0.92em / 1em

- **H4 - Subsection Title**:
  - Font: Poppins
  - Weight: 700
  - Size: 20px / 25px
  - Line Height: 1.2em / 1.12em

#### Body Text
- **Body Large**:
  - Font: Poppins
  - Weight: 500 / 600
  - Size: 16px / 17px / 18px
  - Line Height: 1.5em / 1.25em

- **Body Medium**:
  - Font: Poppins
  - Weight: 400 / 500
  - Size: 14px
  - Line Height: 1.5em / 1.43em

- **Body Small**:
  - Font: Poppins
  - Weight: 400 / 500
  - Size: 12px / 13px
  - Line Height: 1.5em / 1.38em

- **Body Extra Small**:
  - Font: Poppins
  - Weight: 500
  - Size: 10px / 11px
  - Line Height: 1.5em

#### Special Text Styles
- **Button Text**:
  - Font: Poppins
  - Weight: 600 / 700
  - Size: 16px / 19px
  - Line Height: 1.5em / 1.26em

- **Label Text**:
  - Font: Poppins
  - Weight: 500
  - Size: 13px / 14px
  - Line Height: 1.5em

- **Placeholder Text**:
  - Font: Poppins
  - Weight: 400
  - Size: 14px
  - Line Height: 1.5em
  - Color: `#A3A0A0`

---

## SPACING SYSTEM

### Standard Spacing Values
- **4px** - Minimal spacing
- **8px** - Small spacing
- **12px** - Default border radius for inputs/cards
- **16px** - Standard padding/margin
- **20px** - Medium spacing
- **24px** - Large spacing
- **28px** - Extra large spacing
- **32px** - Section spacing
- **48px** - Large section spacing

### Component-Specific Spacing
- **Input Field Height**: 48px
- **Button Height**: 47px / 48px
- **Card Padding**: 16px - 24px
- **Section Gap**: 20px - 32px
- **Page Padding**: 20px - 25px horizontal

---

## BORDER RADIUS

- **Small**: 8px - Small elements, inputs
- **Medium**: 12px - Cards, buttons, inputs (most common)
- **Large**: 16px - Large cards, modals
- **Extra Large**: 20px - Header bottom corners
- **Pill**: 28px / 30px - Pill-shaped buttons
- **Rounded**: 39px - Large rounded buttons
- **Circle**: 50% - Circular elements

### Special Border Radius
- **Rounded Left**: `0px 50px 50px 0px` - Left-rounded cards
- **Rounded Top**: `10px 10px 0px 0px` - Top-rounded elements
- **Rounded Bottom**: `0px 0px 20px 20px` - Bottom-rounded headers

---

## SHADOWS & EFFECTS

### Box Shadows
- **Card Shadow (Standard)**:
  - `0px 2px 8px 0px rgba(0, 0, 0, 0.05)`

- **Button Shadow (Primary)**:
  - `0px 4px 12px 0px rgba(97, 170, 115, 1)` - Green button shadow

- **Card Shadow (Elevated)**:
  - `0px 4px 10px 0px rgba(0, 0, 0, 0.1)`

- **Image Shadow**:
  - `5px 4px 3px 0px rgba(0, 0, 0, 0.25)` / `6px 4px 6px 0px rgba(0, 0, 0, 0.25)`

- **Button Shadow (Accent)**:
  - `0px 4px 4px 0px rgba(95, 170, 116, 1)`

---

## PAGE STRUCTURES

### 1. LOGIN PAGE

#### Layout
- **Background**: `#EDEDED`
- **Full screen layout** with decorative elements

#### Components

**Header Section**:
- Decorative background elements (SVG frames)
- **Logo/Brand Name**: "DecorMate"
  - Font: Agbalumo, 24px, Weight 400
  - Color: `#675D50`
  - Position: Centered horizontally, top section

**Welcome Section**:
- **Heading**: "Welcome Back"
  - Font: Agbalumo, 28px, Weight 400
  - Color: `#000000`
- **Subheading**: "Sign in to continue decorating"
  - Font: Inika, 19px, Weight 400
  - Color: `#6D6D6D`

**Form Fields**:
- **Email Field**:
  - Label: "Email" (Poppins, 14px, Weight 500, Color `#242424`)
  - Input: White background (`#FFFFFF`), border `#9EA484`, 1px, border-radius 12px
  - Placeholder: "Enter your email" (Poppins, 14px, Weight 400, Color `#A3A0A0`)
  - Height: 48px
  - Padding: Horizontal 16px

- **Password Field**:
  - Label: "Password" (Poppins, 14px, Weight 500, Color `#242424`)
  - Input: White background, border `#9EA484`, 1px, border-radius 12px
  - Placeholder: "Enter your password" (Poppins, 14px, Weight 400, Color `#A3A0A0`)
  - Height: 48px
  - Eye icon on the right (20x20px)
  - "Forget Password?" link below (Poppins, 12px, Weight 500, Color `#676B55`)

**Primary Button**:
- **Sign In Button**:
  - Background: `#61AA73`
  - Text: "Sign In" (Poppins, 16px, Weight 600, Color `#FFFFFF`)
  - Border-radius: 12px
  - Shadow: `0px 4px 12px 0px rgba(97, 170, 115, 1)`
  - Height: 47px
  - Width: 269px (centered)

**Divider Section**:
- Horizontal line with "Or continue with" text
  - Line color: `#E5D5C8`, 2px height
  - Text: Poppins, 12px, Weight 400, Color `#9EA484`

**Social Login Buttons**:
- **Google Button**:
  - Background: `#FFFFFF`
  - Border: `#E5D5C8`, 2px
  - Border-radius: 12px
  - Shadow: `0px 2px 8px 0px rgba(0, 0, 0, 0.05)`
  - Google icon (20x20px) + "Google" text (Poppins, 14px, Weight 500, Color `#6B3D0C`)
  - Height: 48px

- **Facebook Button**:
  - Same styling as Google button
  - Facebook icon + "Facebook" text

**Footer Link**:
- "Don't have an account? Sign up"
  - Text: Poppins, 12px, Weight 400, Color `#000000`
  - Link: Poppins, 12px, Weight 500, Color `#676B55`

---

### 2. REGISTER/SIGN UP PAGE

#### Layout
- **Background**: `#EDEDED`
- Similar structure to login page

#### Components

**Header Section**:
- Decorative elements
- Logo: "DecorMate" (same as login)

**Heading Section**:
- **Title**: "Create Account"
  - Font: Agbalumo, 22px, Weight 400
  - Color: `#000000`

**Form Fields** (all with same styling as login inputs):
- **First Name**: Label + Input field
- **Last Name**: Label + Input field
- **Email**: Label + Input field
- **Password**: Label + Input field with eye icon
- **Confirm Password**: Label + Input field with eye icon

**Primary Button**:
- **Sign Up Button**:
  - Background: `#61AA73`
  - Text: "Sign Up" (Poppins, 15px, Weight 600, Color `#FFFFFF`)
  - Border-radius: 12px
  - Shadow: `0px 4px 12px 0px rgba(195, 204, 160, 1)`
  - Height: 47px

**Social Login Section**:
- Same as login page (Google, Facebook buttons)

**Footer Link**:
- "Already have an account? Sign in"
  - Same styling as login footer

---

### 3. HOME PAGE

#### Layout
- **Background**: `#EDEDED`
- Scrollable content with header and bottom navigation

#### Components

**Header Section** (Gradient Background):
- Background: Linear gradient from `rgba(171, 196, 170, 1)` to `rgba(237, 237, 237, 1)`
- Border-radius: `0px 0px 20px 20px` (bottom corners)
- Contains:
  - Logo: "DecorMate" (Agbalumo, 24px, Color `#675D50`)
  - Search/Back icon (24x24px)

**Welcome Section**:
- **Heading**: "Welcome, User Name"
  - Font: Agbalumo, 24px, Weight 400
  - Color: `#000000`
- **Subheading**: "Transform your space with style and creativity"
  - Font: Poppins, 14px, Weight 400
  - Color: `#6D6D6D`

**About Us Card**:
- Background: `#FFFFFF`
- Border: `#9EA484`, 1px
- Border-radius: 16px
- Shadow: `0px 2px 8px 0px rgba(0, 0, 0, 0.05)`
- Contains:
  - **Title**: "About Us" (Poppins, 18px, Weight 700, Color `#FFFFFF` on green background)
  - **Content**: "DecorMate helps you transform space. Explore endless design possibilities, get personalized recommendations only with our AI-powered tools."
    - Font: Poppins, 14px, Weight 400, Color `#6D6D6D`
  - **Button**: "Read More"
    - Background: `#61AA73` or `#9EA484`
    - Text: Poppins, 16px, Weight 700, Color `#FFFFFF`
    - Border-radius: 16px

**Your History Section**:
- **Section Header**:
  - Title: "Your History" (Poppins, 20px, Weight 700, Color `#000000`)
  - Link: "View All" (Poppins, 16px, Weight 700, Color `#61AA73` or `#9EA484`)

**History Cards** (List):
- Each card:
  - Background: `#FFFFFF`
  - Border: `#9EA484`, 1px
  - Border-radius: 12px
  - Shadow: `0px 1px 2px 0px rgba(0, 0, 0, 0.1)`
  - Contains:
    - **Thumbnail Image**: 48x36px, border-radius 12px
    - **Title**: e.g., "Living Room Design" (Poppins, 17px, Weight 700, Color `#2A2A2A`)
    - **Date**: e.g., "2 days ago" (Poppins, 12px, Weight 400, Color `#6D6D6D`)
    - **Icon**: Arrow/chevron (20x20px) on right

**Bottom Navigation Bar**:
- Background: `#FFFFFF`
- Border: `#9EA484`, 1px (top)
- Border-radius: `10px 10px 0px 0px` (top corners)
- Height: ~80px
- Contains 5 items:
  - **Home** (Active): Background `#E4FAEB`, Text Color `#61AA73`
  - **Profile**: Text Color `#B0B0B0`
  - **Settings**: Text Color `#B0B0B0`
  - **Market**: Text Color `#B0B0B0`
  - **Floating Action Button**: Circular, `#61AA73`, 55x55px, Shadow `0px 4px 4px 0px rgba(95, 170, 116, 1)`

---

### 4. DASHBOARD/PROFILE PAGE

#### Layout
- **Background**: `#EDEDED`
- Scrollable profile content

#### Components

**Header Section** (Same gradient as home):
- Logo and navigation

**Profile Header**:
- **Profile Picture**:
  - Size: 66x66px or larger
  - Border-radius: Circular or 12px
  - Shadow: `5px 4px 3px 0px rgba(0, 0, 0, 0.25)`
  - Camera icon overlay (48x48px) for editing

- **User Info**:
  - **Name**: e.g., "Ahmed Syam" (Poppins, 24px, Weight 700, Color `#5B3308`)
  - **Email**: e.g., "ahmed@example.com" (Poppins, 16px, Weight 400, Color `#5B3308`)

**Profile Information Cards**:
- **Personal Information Card**:
  - Background: `#FFFFFF`
  - Border: `#9EA484`, 1px
  - Border-radius: 16px
  - Shadow: `0px 2px 8px 0px rgba(0, 0, 0, 0.05)`
  - Contains:
    - **Section Title**: "Personal Information" (Poppins, 18px, Weight 700, Color `#2A2A2A`)
    - **Fields**:
      - First Name: Label + Input (Background `#E5E5E5`, Border-radius 8px)
      - Last Name: Label + Input
      - Email: Label + Input
      - Phone Number: Label + Input

**Account Actions Card**:
- Same card styling
- **Section Title**: "Account Actions" (Poppins, 18px, Weight 700, Color `#2A2A2A`)
- **Action Buttons**:
  - Change account (Border `#E5D5C8`, 2px, Border-radius 12px)
  - Change password
  - Delete Account

**Support & Help Card**:
- Same card styling
- **Section Title**: "Support & Help" (Poppins, 18px, Weight 700, Color `#000000`)
- **Menu Items**:
  - Help Center (with ❓ icon)
  - Contact Support (with 💬 icon)
  - Rate App (with ⭐ icon)
  - Terms & Privacy (with 📄 icon)
  - Each with arrow (>) icon, Color `#A14B00`

**Sign Out Button**:
- Background: `#61AA73`
- Text: "Sign Out" (Poppins, 19px, Weight 800, Color `#FFFFFF`)
- Border-radius: 12px
- Logout icon (32x32px)

**Bottom Navigation**: Same as home page

---

### 5. SETTINGS PAGE

#### Layout
- **Background**: `#EDEDED`
- Similar structure to profile page

#### Components

**Header**: Same gradient header with "Settings" title

**Settings Sections**:

**Notification Settings Card**:
- Background: `#FFFFFF`
- Border-radius: 16px
- **Title**: "Notification Types" (Poppins, 14px, Weight 500, Color `#675D50`)
- **Toggle Switches**:
  - Email (Active: Background `#61AA73`, Border 4px)
  - Mobile
  - In-App

**Other Settings Cards**: Similar structure for various settings

**Bottom Navigation**: Same as other pages

---

### 6. FORGET PASSWORD PAGE

#### Layout
- **Background**: `#EDEDED`

#### Components

**Header**: Same gradient header with "forget password" title

**Reset Card**:
- Background: `#FFFFFF`
- Border-radius: 16px
- Shadow: `0px 2px 8px 0px rgba(0, 0, 0, 0.05)`
- Contains:
  - **Title**: "Reset Your Password" (Poppins, 20px, Weight 700, Color `#000000`)
  - **Description**: "Enter your registered email to receive a reset link." (Poppins, 14px, Weight 400, Color `#6D6D6D`)
  - **Email Field**: Label + Input (same styling as login)
  - **Button**: "Get Code" (Background `#61AA73`, Border-radius 30px)

**Help Section**:
- **Card 1**: Contact Support
  - Background: `#FFFFFF`
  - Border: `#9EA484`, 1px
  - Border-radius: 8px
  - Icon: 💬
  - Title: "Contact Support" (Poppins, 14px, Weight 500, Color `#675D50`)
  - Description: "Get help from our team" (Poppins, 12px, Weight 400, Color `#6B3D0C`)

- **Card 2**: Help Center (similar styling)

**Security Tips Card**:
- Background: `#FFFFFF`
- Border-radius: 12px
- **Title**: "Security Tips" (Poppins, 14px, Weight 600, Color `#000000`)
- **Bullet Points** (Poppins, 12px, Weight 400, Color `#6D6D6D`):
  - • Use a strong, unique password
  - • Don't share your login credentials
  - • Log out from shared devices
  - • Enable two-factor authentication

---

### 7. OTP VERIFICATION PAGE

#### Layout
- **Background**: `#EDEDED`

#### Components

**Header**: Gradient header with logo

**OTP Section**:
- **Title**: "OTP Verification" (Poppins, 27px, Weight 400, Color `#2A2A2A`)
- **Description**: "Enter the 6-digit code sent to your email" (Poppins, 16px, Weight 400, Color `#6D6D6D`)

**OTP Input Fields**:
- 6 individual input boxes
- Background: `#FFFFFF`
- Border: `#E5D5C8`, 2px
- Border-radius: 12px
- Shadow: `0px 2px 8px 0px rgba(0, 0, 0, 0.05)`
- Size: ~40x40px each
- Text: Center aligned, large font (20px, Weight 600, Color `#000000`)

**Verify Button**:
- Background: `#5FAA74` (gradient)
- Text: "Verify" (Poppins, 20px, Weight 700, Color `#FFFFFF`)
- Border-radius: 12px
- Shadow: `0px 4px 4px 0px rgba(95, 170, 116, 1)`

**Resend Section**:
- "Didn't receive the code?" (Poppins, 17px, Weight 400, Color `#676B55`)
- "Resend Code" link (Poppins, 17px, Weight 600, Color `#647356`)

---

### 8. INPUT METHOD SELECTION PAGES

#### Layout
- **Background**: `#EDEDED`

#### Components

**Header**: Gradient header with "DecorMate" logo

**Main CTA Button** (Top):
- **"Start Decorating" Button**:
  - Background: `#5FAA74`
  - Text: "Start Decorating" (Poppins, 19px, Weight 600, Color `#FFFFFF`)
  - Border-radius: 39px (pill shape)
  - Position: Top of page

**Heading Section**:
- **Title**: "Let's Get Started!" (Agbalumo, 30px, Weight 400, Color `#000000`)
- **Subtitle**: "Choose your preferred method" (Poppins, 14px, Weight 400, Color `#6D6D6D`)

**Method Selection Cards** (Vertical list):

1. **Floor Plan Card** (Selected):
   - Background: Gradient from `rgba(121, 213, 147, 1)` to `rgba(63, 111, 76, 1)`
   - Border-radius: `0px 50px 50px 0px` (left rounded)
   - Shadow: `0px 8px 30px 0px rgba(0, 0, 0, 0.08)`
   - Contains:
     - Icon (left side)
     - **Title**: "Floor Plan" (Agbalumo, 26px, Weight 400, Color `#ECFFE5`)
     - **Description**: "Upload your room layout" (Poppins, 17px, Weight 600, Color `#3F6F4C`)

2. **Prompt Design Card** (Selected variant):
   - Background: `#FFFFFF`
   - Border: `#9EA484`, 1px
   - Border-radius: `0px 50px 50px 0px`
   - Contains:
     - **Title**: "Prompt Design" (Agbalumo, 26px, Weight 400, Color `#2E2E2E`)
     - **Icon Circle**: Background `#E9E7FB`, Pencil icon
     - **Description**: "Describe your vision" (Poppins, 17px, Weight 600, Color `#ABA9A9`)

3. **Scan Room Card**:
   - Similar styling to unselected cards
   - **Title**: "Scan Room"
   - **Description**: "Upload your room photos"

4. **Depth Detection Card** (Selected variant):
   - Background: Gradient (when selected)
   - **Title**: "Depth Detection" (Agbalumo, 24px, Weight 400, Color `#ECFFE5` or `#2E2E2E`)
   - **Description**: "Analyzes your space" (Poppins, 17px, Weight 600, Color `#3F6F4C` or `#ABA9A9`)

**Bottom Navigation**: Same as other pages

---

## COMPONENT SPECIFICATIONS

### Buttons

#### Primary Button
- Background: `#61AA73`
- Text: Poppins, 16px, Weight 600, Color `#FFFFFF`
- Border-radius: 12px
- Shadow: `0px 4px 12px 0px rgba(97, 170, 115, 1)`
- Height: 47px - 48px
- Padding: Horizontal 24px

#### Secondary Button
- Background: `#FFFFFF`
- Border: `#E5D5C8`, 2px
- Text: Poppins, 14px, Weight 500, Color `#6B3D0C`
- Border-radius: 12px
- Shadow: `0px 2px 8px 0px rgba(0, 0, 0, 0.05)`
- Height: 48px

#### Pill Button
- Background: `#5FAA74`
- Text: Poppins, 19px, Weight 600, Color `#FFFFFF`
- Border-radius: 39px
- Height: ~60px

### Input Fields

#### Standard Input
- Background: `#FFFFFF`
- Border: `#9EA484`, 1px
- Border-radius: 12px
- Height: 48px
- Padding: Horizontal 16px
- Placeholder: Poppins, 14px, Weight 400, Color `#A3A0A0`
- Label: Poppins, 13px-14px, Weight 500, Color `#242424`
- Focus state: Border color change (if applicable)

#### Input with Icon
- Same as standard input
- Icon positioned on right (20x20px)
- Icon color: `#675D50` or `#000000`

### Cards

#### Standard Card
- Background: `#FFFFFF`
- Border: `#9EA484`, 1px (optional)
- Border-radius: 12px / 16px
- Shadow: `0px 2px 8px 0px rgba(0, 0, 0, 0.05)`
- Padding: 16px - 24px

#### Elevated Card
- Same as standard card
- Shadow: `0px 4px 10px 0px rgba(0, 0, 0, 0.1)`

#### History Card
- Background: `#FFFFFF`
- Border: `#9EA484`, 1px
- Border-radius: 12px
- Shadow: `0px 1px 2px 0px rgba(0, 0, 0, 0.1)`
- Contains: Image thumbnail, title, date, arrow icon

### Navigation

#### Bottom Navigation Bar
- Background: `#FFFFFF`
- Border: `#9EA484`, 1px (top)
- Border-radius: `10px 10px 0px 0px`
- Height: ~80px
- Items: 5 navigation items
- Active state: Background `#E4FAEB`, Text `#61AA73`
- Inactive state: Text `#B0B0B0`

#### Floating Action Button
- Background: `#61AA73`
- Size: 55x55px
- Border-radius: 50% (circle)
- Shadow: `0px 4px 4px 0px rgba(95, 170, 116, 1)`
- Icon: White, centered

### Header

#### Gradient Header
- Background: Linear gradient from `rgba(171, 196, 170, 1)` to `rgba(237, 237, 237, 1)`
- Border-radius: `0px 0px 20px 20px` (bottom corners)
- Height: ~100px
- Contains: Logo, navigation icons

---

## RESPONSIVE BEHAVIOR

### Breakpoints
- **Mobile**: 393px width (primary design)
- All components should be responsive and adapt to screen sizes

### Layout Rules
- **Horizontal Padding**: 20px - 25px on mobile
- **Vertical Spacing**: 20px - 32px between sections
- **Card Width**: Full width minus padding (345px - 372px on 393px screen)
- **Input Width**: Full width minus padding
- **Button Width**: Can be full width or centered with fixed width (269px - 345px)

### Scrolling
- All pages are vertically scrollable
- Fixed header and bottom navigation (sticky)
- Content scrolls between header and navigation

---

## INTERACTIONS & ANIMATIONS

### Button States
- **Default**: As specified
- **Hover**: Slight elevation (shadow increase)
- **Active/Pressed**: Slight scale down (0.98)
- **Disabled**: Opacity 0.5, no interaction

### Input States
- **Default**: As specified
- **Focus**: Border color may change to primary green
- **Error**: Red border (if applicable)
- **Success**: Green border (if applicable)

### Card Interactions
- **Tap/Press**: Slight scale (0.98) or elevation change
- **Selection**: Background color change, border highlight

### Transitions
- All interactions should have smooth transitions (0.2s - 0.3s ease)
- Color changes, shadows, and transforms should animate smoothly

---

## ICONS & ASSETS

### Icon Specifications
- **Size**: 20x20px, 24x24px, 32x32px, 48x48px (context-dependent)
- **Color**: `#675D50`, `#000000`, `#FFFFFF` (context-dependent)
- **Style**: Outline or filled (as per design)

### Common Icons
- Eye (show/hide password)
- Arrow/Chevron (navigation)
- Camera (profile picture)
- Lock (security)
- Search
- Home, Profile, Settings, Market (navigation)
- Social media icons (Google, Facebook)

---

## ACCESSIBILITY CONSIDERATIONS

### Color Contrast
- Ensure WCAG AA compliance for text on backgrounds
- Primary text on white: `#000000` or `#242424` (sufficient contrast)
- Secondary text: `#6D6D6D` (check contrast ratios)

### Touch Targets
- Minimum 44x44px for interactive elements
- Buttons: 47px - 48px height (compliant)
- Input fields: 48px height (compliant)

### Text Readability
- Minimum font size: 12px (body text)
- Line height: 1.5em for body text (improves readability)

---

## IMPLEMENTATION NOTES

### Technology Stack Recommendations
- **Framework**: React Native (mobile) or React (web)
- **Styling**: StyleSheet (React Native) or CSS Modules/Tailwind (web)
- **Icons**: React Native Vector Icons or similar
- **Fonts**: Import Agbalumo, Poppins, and Inika from Google Fonts or local assets

### Code Structure
- Component-based architecture
- Reusable components: Button, Input, Card, Header, BottomNav
- Theme/constants file for colors, typography, spacing
- Responsive utilities for layout

### Assets Required
- Logo/Icon assets
- Decorative background elements (SVG)
- Social media icons
- Navigation icons
- Profile placeholder images

---

## FINAL CHECKLIST

Before implementation, ensure:
- [ ] All colors match exact hex values
- [ ] Typography uses correct fonts, sizes, weights, line heights
- [ ] Spacing follows the specified system
- [ ] Border radius matches specifications
- [ ] Shadows and effects are applied correctly
- [ ] All pages are implemented with correct structure
- [ ] Components are reusable and consistent
- [ ] Responsive behavior works across screen sizes
- [ ] Interactions and animations are smooth
- [ ] Accessibility standards are met

---

**End of Design Specification**

This document contains all necessary information to recreate the DecorMate app UI exactly as designed in Figma. Use this as a reference for front-end implementation.

