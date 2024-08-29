# What is .SAT?
`.SAT` is a custom format for MERCURY/SATURN chart files. It's designed to be more readable than `.MER` and to reduce unnecessary information.
# Structure
`.SAT` files consist of three parts: Metadata, Gimmicks, and Objects.
Data is defined line-by-line. That means two objects cannot be defined on the same line.

A file begins with all necessary Metadata tags that describe the chart and song.
Then, the list of Gimmicks is declared with the Metadata tag `@GIMMICKS`, and each line describes one gimmick.
Then, the list of Objects is declared with the Metadata tag `@OBJECTS`, and each line describes one object.

# Metadata Tags
Metadata Tags must begin with an `@`. They define things such as the Title, Artist, Chart Author, etc. All valid tags are listed below.

`@SAT_VERSION` `[Int]`
	Version of the file format

`@VERSION` `[Int]`
	Version of the chart. Needs to be updated manually by the chart author.

`@TITLE` `[String]`
	Title of the charted song.

`@RUBI` `[String]`
	Title of the charted song in a sortable format. Preferred characters are the Latin alphabet and Hiragana. The Title `神` would have a Rubi of `かみ`. The Title `PANIC POP ☆ FESTIVAL!!!` would have a Rubi of `PANIC POP FESTIVAL`

`@ARTIST` `[String]`
	Artist of the charted song.

`@AUTHOR` `[String]`
	Chart author name.

`@DIFF` `[Int]`
	Number ranging from 0 to 4 to define the difficulty category.
	
	0 -> Normal
	1 -> Hard
	2 -> Expert
	3 -> Inferno
	4 -> Abyss

`@LEVEL` `[Int]`
	Difficulty level of a chart. Also known as a "Constant" or "Chart Constant".
	Should abide by similar rating guidelines as official WACCA charts.

`@CLEAR` `[Float]`
	Clear threshold for a chart. `0.83` by default.

`@BPM_TEXT` `[String]`
	BPM text that is displayed on the song select screen. Does not affect gameplay BPM.

`@PREVIEW_START `[Float]`
	Start timestamp for the song and chart preview.

`@PREVIEW_TIME `[Float]`
	Length for the song and chart preview.

`@BGM` `[String]`
	Local file path to background music file.

`@BGM_OFFSET` `[Float]`
	**DEPRECATED.** *Set up your audio files properly!*
	Audio offset in seconds. 
	A positive offset will make the audio earlier than the chart.
	A negative offset will make the audio later than the chart.

`@BGA` `[String]`
	Local file path to background animation file.

`@BGA_OFFSET` `[Float]`
	**DEPRECATED.** *Set up your video files properly!*
	Video offset in seconds. 
	A positive offset will make the video earlier than the chart.
	A negative offset will make the video later than the chart.

`@JACKET` `[String]`
	Local file path to jacket file. Jackets specific to one difficulty similar to SDVX are also possible.

`@COMMENTS`
	Declares a list of Editor-Only Comments.

`@GIMMICKS`
	 Declares a list of Gimmicks.

`@OBJECTS`
	Declares a list of Objects.

# Comments, Gimmicks, and Objects
Gimmicks and Objects are the actual content of a chart. They follow a similar structure, just with different amounts and types of data per line.

Both Gimmicks and Objects begin with the same data - a Timestamp and an Index.
The Index is only for debugging and counts up by one for each Gimmick or Objects. Gimmicks and Objects each have their own counter, so after counting up for Gimmicks, the index will start back at 1 on the first Object.

A Timestamp in `.SAT` is defined by two integers, a Measure and a Tick.
That means intervals depend on the current BPM and are not a fixed length.
##### Measure
A Measure in `.SAT` is comparable to a bar/measure in musical notation.
In 4/4, a measure would be four beats long. 

##### Tick
A Tick in `.SAT` is 1/1920th of a measure. It ranges from 0 to 1919.

## Comments
Comments are editor-only and give chart authors a way to communicate for asynchronous collaborations, or to add bookmarks to a chart.

After the Timestamp and Index, a Comment just stores the written text. Line breaks are not allowed in comments.
## Gimmicks
Gimmicks are global gameplay effects that modify the game visually without providing any content to play.

After the Timestamp and Index, a Gimmick's "Type" is declared. Unlike `.mer` where an ID is used, `.SAT` uses words to declare types. Some Gimmicks also require additional values to be provided.

All valid Gimmick Types and the values they take are listed below.

`BPM`
	A BPM Change. Requires a single Float for the new BPM.

`TIMESIG`
	 A Time Signature Change. Requires two Integers for the new Time Signature Upper and Lower.

`HISPEED`
	A Hi-Speed Change, also known as "Soflan" or Scroll Speed. Requires a single Float for the new speed. `1.0` by default.

`STOP_START`
	The beginning of a Stop/Freeze. Completely stops note scrolling until `STOP_END` is reached. Does not require any additional data.

`STOP_END`
	The end of a Stop/Freeze. Continues note scrolling with the last set Hi-Speed.
	Does not require any additional data.

`REV_START`
	The beginning of a Reverse. Notes begin to scroll backwards from the judgement line towards the center until `REVERSE_END` is reached.
	Does not require any additional data.

`REV_END`
	The end of a Reverse. Any visible notes will slow down, then start scrolling forwards again.

`REV_ZONE_END`
	The end of the Reverse Note Zone. All Notes between `REVERSE_ZONE_END` and `REVERSE_END` are copied, reversed and then scrolled backwards during the duration of the reverse.

`CHART_END`
	 Defines the end of the Chart, when note scrolling stops and the game displays either "CLEAR" or "FAIL". There must only be one `CHART_END` gimmick in the chart.

## Objects
Objects are contained to the playfield. They include Notes and Masks.
After the Timestamp and Index, an Object's Position, Size and Type is declared.

##### Position
The Position is an Integer ranging from 0 to 59. It loops back to 0 once it reaches 60.
When comparing to a clock, Position 0 is at 3 o' clock. It then goes counterclockwise in 6° steps. It defines the "Left", or most-clockwise edge of an Object.

##### Size
The Size is an Integer ranging from 1 to 60. It defines how big the arc of an Object is.

##### Modifiers 
Modifiers are variants of an Object Type. They're "attached" to the Type with a period.
Most Object Types have modifiers for Bonus or R-Note variants.
All objects have an implied `.NORMAL` modifier for their default variant. It can optionally be written, but is generally excluded to save space.

All valid Object Types and their Modifiers are listed below.

`TOUCH`
`TOUCH.BONUS`
`TOUCH.RNOTE`

`SLIDE_CW`
`SLIDE_CW.BONUS`
`SLIDE_CW.RNOTE`

`SLIDE_CCW`
`SLIDE_CCW.BONUS`
`SLIDE_CCW.RNOTE`

`SNAP_FW`
`SNAP_FW.RNOTE`
	Forward Snap Notes do not have a `.BONUS` variant.

`SNAP_BW`
`SNAP_BW.RNOTE`
	Backward Snap Notes do not have a `.BONUS` variant.
``
`CHAIN`
`CHAIN.RNOTE`
	Chain Notes do not have a `.BONUS` variant.

`HOLD_START`
`HOLD_START.RNOTE`
	Hold Start Notes do not have a `.BONUS` variant.

`HOLD_POINT`
`HOLD_POINT.NR`
	 `.NR` stands for No-Render and replaces the render flag from `.mer`.

`HOLD_END`

`MASK_ADD.CW`
`MASK_ADD.CCW`
`MASK_ADD.CENTER`
	Mask Add Objects do not have a valid `.NORMAL` variant.

`MASK_SUB.CW`
`MASK_SUB.CCW`
`MASK_SUB.CENTER`
	Mask Add Objects do not have a valid `.NORMAL` variant.
##### Chroma
`.SAT` allows Notes to be re-colored by adding a hex code after the Type declaration.
Alpha is ignored, so the only valid hex code format is `#RRGGBB`.
