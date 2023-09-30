use std::{collections::HashMap, ops::Deref, rc::Rc, str::FromStr};

use i18n_embed::{
    fluent::{fluent_language_loader, FluentLanguageLoader},
    WebLanguageRequester,
};
use i18n_embed_fl::fl;
use icu::{
    calendar::{types::IsoWeekday, week::WeekCalculator},
    locid::Locale,
};
use implicit_clone::unsync::IArray;
use js_sys::{Array, Date, Intl::DateTimeFormat, JsString, Object, Reflect};
use rust_embed::RustEmbed;
use serde::Deserialize;
use wasm_bindgen::prelude::*;
use web_sys::window;
use yew::prelude::*;

use crate::calendar::Calendar;

mod about;
mod calendar;
mod event;
mod event_detail;
mod render;
mod time_slot;

type LanguageLoader = Rc<RefEqual<FluentLanguageLoader>>;

#[derive(Clone, Debug, Deserialize, Eq, PartialEq)]
struct Days<T> {
    sunday: T,
    monday: T,
    tuesday: T,
    wednesday: T,
    thursday: T,
    friday: T,
    saturday: T,
}

impl<T> Days<T> {
    fn as_ref_array<R>(&self, first_day_of_week: IsoWeekday) -> [&R; 7]
    where
        R: ?Sized,
        T: AsRef<R>,
    {
        let mut days = [
            self.sunday.as_ref(),
            self.monday.as_ref(),
            self.tuesday.as_ref(),
            self.wednesday.as_ref(),
            self.thursday.as_ref(),
            self.friday.as_ref(),
            self.saturday.as_ref(),
        ];
        days.rotate_left((first_day_of_week as usize + 7 - IsoWeekday::Sunday as usize) % 7);
        days
    }

    fn as_clone_array(&self, first_day_of_week: IsoWeekday) -> [T; 7]
    where
        T: Clone,
    {
        let mut days = [
            self.sunday.clone(),
            self.monday.clone(),
            self.tuesday.clone(),
            self.wednesday.clone(),
            self.thursday.clone(),
            self.friday.clone(),
            self.saturday.clone(),
        ];
        days.rotate_left((first_day_of_week as usize + 7 - IsoWeekday::Sunday as usize) % 7);
        days
    }
}

#[derive(Deserialize)]
struct Data {
    meta: Rc<MetadataRoot>,
}

#[derive(Deserialize, Eq, PartialEq)]
struct MetadataRoot {
    #[serde(flatten)]
    meta: Metadata,
    lang: HashMap<String, Metadata>,
}

#[derive(Deserialize, Eq, PartialEq)]
struct Metadata {
    title: Option<AttrValue>,
    desc: Option<AttrValue>,
    link: Option<AttrValue>,
}

#[function_component]
fn App(props: &AppProps) -> Html {
    let first_day_of_week = props.first_day_of_week;

    const COLORS: &[AttrValue] = &[
        AttrValue::Static("#c0768c"),
        AttrValue::Static("#5f986e"),
        AttrValue::Static("#8f84bc"),
        AttrValue::Static("#9e8a53"),
        AttrValue::Static("#3296bb"),
        AttrValue::Static("#c17871"),
        AttrValue::Static("#3a9a8a"),
        AttrValue::Static("#af7ba8"),
        AttrValue::Static("#81925a"),
        AttrValue::Static("#638ec3"),
        AttrValue::Static("#b5805c"),
        AttrValue::Static("#179aa6"),
    ];
    let colors = IArray::Static(COLORS);

    let fallback = html! {{fl!(props.language_loader, "loading")}};
    let data_url = AttrValue::Static("data.json");
    html! {
        <ContextProvider<LanguageLoader> context={props.language_loader.clone()}>
            <Suspense fallback={fallback}>
                <Calendar
                    data_url={data_url}
                    first_day_of_week={first_day_of_week}
                    colors={colors}
                    day_names={props.day_names.clone()}
                    language={props.language.clone()}
                    time_zone={props.time_zone.clone()} />
            </Suspense>
            <div class="footer">
                <div class="spacer"></div>
                <div class="time-zone">{props.time_zone.clone()}</div>
            </div>
        </ContextProvider<LanguageLoader>>
    }
}

#[derive(Eq, PartialEq, Properties)]
struct AppProps {
    language_loader: Rc<RefEqual<FluentLanguageLoader>>,
    language: AttrValue,
    time_zone: AttrValue,
    first_day_of_week: IsoWeekday,
    day_names: Days<AttrValue>,
}

#[derive(Clone, Copy)]
struct RefEqual<T>(T);

impl<T> PartialEq for RefEqual<T> {
    fn eq(&self, other: &Self) -> bool {
        std::ptr::eq(&self.0, &other.0)
    }
}

impl<T> Eq for RefEqual<T> {}

impl<T> Deref for RefEqual<T> {
    type Target = T;

    fn deref(&self) -> &Self::Target {
        &self.0
    }
}

#[derive(RustEmbed)]
#[folder = "i18n/"]
struct Localizations;

fn main() {
    let language_loader: FluentLanguageLoader = fluent_language_loader!();
    let requested_languages = WebLanguageRequester::requested_languages();
    _ = i18n_embed::select(&language_loader, &Localizations, &requested_languages);

    let language = requested_languages
        .get(0)
        .map(|l| AttrValue::from(l.language.as_str().to_owned()))
        .unwrap_or(AttrValue::Static("ja"));

    let options =
        js_sys::Intl::DateTimeFormat::new(&Array::new(), &Object::new()).resolved_options();
    let time_zone = Reflect::get(&options, &JsString::from("timeZone"))
        .ok()
        .as_ref()
        .and_then(JsValue::as_string)
        .map(AttrValue::from)
        .unwrap_or(AttrValue::Static("Asia/Tokyo"));

    // There is a draft API for getting this, but it's not supported by Firefox (and therefore also
    // not supported by js_sys), so we'll map the browser locale to an ICU locale (unfortunately
    // losing user customization in the process…) and then resolve it through ICU.
    let week_calculator = web_sys::window()
        .into_iter()
        .flat_map(|window| window.navigator().languages())
        .filter_map(|l| l.as_string())
        .filter_map(|l| Locale::from_str(&l).ok())
        .find_map(|l| WeekCalculator::try_new(&l.into()).ok())
        .unwrap_or_else(|| WeekCalculator::try_new(&Locale::UND.into()).unwrap());

    let format_config = {
        let config = Object::new();
        Reflect::set(&config, &JsString::from("weekday"), &JsString::from("long")).unwrap();
        config
    };
    // It might seem silly to explicitly specify the default languages here.
    // Firefox has separate language settings for the browser vs pages.
    // Firefox 117 uses the browser language settings instead of the page language setting if `[]` is passed.
    let formatter = DateTimeFormat::new(&window().unwrap().navigator().languages(), &format_config);
    let format = formatter.format();
    let get_day_name = |day: IsoWeekday| -> AttrValue {
        format
            .call1(
                &formatter,
                &Date::new_with_year_month_day(1970, 1, 1 + day as i32),
            )
            .unwrap()
            .as_string()
            .unwrap()
            .into()
    };
    let day_names = Days {
        sunday: get_day_name(IsoWeekday::Sunday),
        monday: get_day_name(IsoWeekday::Monday),
        tuesday: get_day_name(IsoWeekday::Tuesday),
        wednesday: get_day_name(IsoWeekday::Wednesday),
        thursday: get_day_name(IsoWeekday::Thursday),
        friday: get_day_name(IsoWeekday::Friday),
        saturday: get_day_name(IsoWeekday::Saturday),
    };

    yew::Renderer::<App>::with_props(AppProps {
        language_loader: Rc::new(RefEqual(language_loader)),
        language,
        time_zone,
        first_day_of_week: week_calculator.first_weekday,
        day_names,
    })
    .render();
}
