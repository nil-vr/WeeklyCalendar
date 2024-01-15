use std::{fmt::Display, rc::Rc};

use icu::calendar::types::IsoWeekday;
use implicit_clone::unsync::IArray;
use serde::Deserializer;
use serde::{de::Visitor, Deserialize, Serialize};
use wasm_bindgen::{prelude::*, throw_str};
use yew::prelude::*;
use yew::suspense::{use_future_with, SuspensionResult, UseFutureHandle};
use yew::virtual_dom::VText;

use crate::Days;

#[derive(Deserialize, Eq, PartialEq)]
pub(super) struct TimeSlot {
    pub time: MinuteTime,
    pub days: Days<Vec<Rc<EventOccurrence>>>,
}

#[derive(Deserialize, Eq, PartialEq)]
#[serde(rename_all = "camelCase")]
pub(super) struct EventOccurrence {
    pub id: u16,

    pub name: AttrValue,
    pub base_name: AttrValue,
    pub duration: MinuteTime,
    #[serde(flatten)]
    pub info: EventInfo,

    pub platforms: Vec<Platform>,
    #[serde(default)]
    pub canceled: bool,
    #[serde(default = "default_true")]
    pub confirmed: bool,

    #[serde(deserialize_with = "deserialize_weekday")]
    pub day: IsoWeekday,
}

fn deserialize_weekday<'de, D>(deserializer: D) -> Result<IsoWeekday, D::Error>
where
    D: Deserializer<'de>,
{
    struct WeekdayVisitor;
    impl<'de> Visitor<'de> for WeekdayVisitor {
        type Value = IsoWeekday;

        fn expecting(&self, formatter: &mut std::fmt::Formatter) -> std::fmt::Result {
            write!(formatter, "An integer")
        }

        fn visit_u64<E>(self, v: u64) -> Result<Self::Value, E>
        where
            E: serde::de::Error,
        {
            Ok(IsoWeekday::from(v as usize))
        }
    }
    deserializer.deserialize_u64(WeekdayVisitor)
}

fn default_true() -> bool {
    true
}

#[derive(Clone, Deserialize, Eq, PartialEq)]
pub(super) struct EventInfo {
    pub poster: Option<Poster>,
    pub web: Option<AttrValue>,
    pub discord: Option<AttrValue>,
    pub group: Option<AttrValue>,
    pub hashtag: Option<Hashtag>,
    pub twitter: Option<AttrValue>,
    #[serde(default)]
    pub join: Vec<Named>,
    pub world: Option<Named>,
    pub weeks: Option<Vec<u8>>,
    pub description: Option<AttrValue>,
}

#[derive(Clone, Copy, Deserialize, Eq, PartialEq)]
pub(super) struct Poster {
    #[serde(rename = "n")]
    pub number: u8,
    #[serde(rename = "w")]
    pub width: u16,
    #[serde(rename = "h")]
    pub height: u16,
}

#[derive(Clone, Deserialize, Eq, PartialEq)]
#[serde(untagged)]
pub enum Hashtag {
    Safe(AttrValue),
    Escaped {
        display: AttrValue,
        escaped: AttrValue,
    },
}

impl Hashtag {
    pub fn display(&self) -> &AttrValue {
        match self {
            Hashtag::Safe(v) => v,
            Hashtag::Escaped { display, .. } => display,
        }
    }

    pub fn escaped(&self) -> &AttrValue {
        match self {
            Hashtag::Safe(v) => v,
            Hashtag::Escaped { escaped, .. } => escaped,
        }
    }
}

#[derive(Clone, Deserialize, Eq, PartialEq)]
pub(super) struct Named {
    pub name: AttrValue,
    pub id: AttrValue,
}

#[derive(Clone, Copy, Deserialize, Eq, PartialEq)]
#[serde(rename_all = "kebab-case")]
pub(super) enum Platform {
    Pc,
    Quest,
}

#[derive(Clone, Copy, Eq, Ord, PartialEq, PartialOrd)]
pub(super) struct MinuteTime(pub u16);

impl<'de> Deserialize<'de> for MinuteTime {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        struct IntVisitor;
        impl<'de> Visitor<'de> for IntVisitor {
            type Value = MinuteTime;

            fn expecting(&self, formatter: &mut std::fmt::Formatter) -> std::fmt::Result {
                write!(formatter, "an integer number of minutes between 0 and 1440")
            }

            fn visit_u64<E>(self, v: u64) -> Result<Self::Value, E>
            where
                E: serde::de::Error,
            {
                if v > 1440 {
                    Err(E::custom("Time is greater than one day"))
                } else {
                    Ok(MinuteTime(v as u16))
                }
            }
        }
        deserializer.deserialize_u16(IntVisitor)
    }
}

impl Display for MinuteTime {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let hours = self.0 / 60;
        let minutes = self.0 % 60;
        write!(f, "{hours:02}:{minutes:02}")
    }
}

impl ToHtml for MinuteTime {
    fn to_html(&self) -> Html {
        Html::VText(VText::new(self.to_string()))
    }
}

#[wasm_bindgen]
extern "C" {
    async fn renderData(input: &str, config: &str) -> JsValue;
}

#[derive(Clone, Eq, PartialEq)]
pub(super) struct RenderParams {
    pub data: AttrValue,
    pub config: RenderConfig,
}

#[derive(Clone, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub(super) struct RenderConfig {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub language: Option<AttrValue>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub time_zone: Option<AttrValue>,
}

#[hook]
pub(super) fn use_rendered(
    input: RenderParams,
) -> SuspensionResult<UseFutureHandle<IArray<Rc<TimeSlot>>>> {
    use_future_with(input, |d| async move {
        let config = serde_json::to_string(&d.config).unwrap();

        let f = renderData(&d.data, &config);
        let data = f
            .await
            .as_string()
            .expect_throw("renderData didn't return a string");
        let mut data: Vec<Rc<TimeSlot>> = match serde_json::from_str(&data) {
            Ok(data) => data,
            Err(e) => throw_str(&format!("renderData returned unexpected data: {e}")),
        };
        data.sort_unstable_by(|a, b| a.time.cmp(&b.time));
        IArray::from(data)
    })
}
